using System;
using System.Text.Json;
using System.Threading.Tasks;
using DarivaBIM.Application.DTOs.Family;
using DarivaBIM.Infrastructure.Api.Clients;
using DarivaBIM.Infrastructure.Persistence.Cache;
using DarivaBIM.Plugin.Features.FamiliesImporter;
using DarivaBIM.Sidecar.Ipc;
using DarivaBIM.Sidecar.Ipc.Methods;

namespace DarivaBIM.Plugin.Sidecar
{
    /// <summary>
    /// Recebe mensagens vindas do sidecar via pipe e as traduz em chamadas ao
    /// fluxo de import de familias do plugin. Mantem uma instancia unica do
    /// <see cref="ImportFamilyExternalEvent"/> reutilizada entre requests —
    /// criar um <c>ExternalEvent</c> por chamada seria desperdicio (a API do
    /// Revit tem teto de event handlers registrados por sessao).
    ///
    /// O dispatcher e thread-safe pra leitura, mas serializa o RAISE do
    /// ExternalEvent: o handler so processa um pedido por vez, entao mandar
    /// dois Raise antes do primeiro completar perderia o request mais antigo.
    /// </summary>
    public sealed class ImportFamilyDispatcher
    {
        private readonly ImportFamilyExternalEvent _externalEvent;
        private readonly FamilyDownloadService _downloadService;
        private readonly FamilyCacheService _cacheService;
        // Lock async pra serializar Raise + await-Completed (uma importacao
        // por vez, conforme contrato do handler).
        private readonly System.Threading.SemaphoreSlim _raiseLock = new(1, 1);

        public ImportFamilyDispatcher()
        {
            _externalEvent = new ImportFamilyExternalEvent();
            _downloadService = new FamilyDownloadService();
            _cacheService = new FamilyCacheService();
        }

        public async Task<IpcResponse> DispatchAsync(IpcRequest request)
        {
            return request.Method switch
            {
                "ping" => HandlePing(request),
                "importFamily" => await HandleImportFamilyAsync(request).ConfigureAwait(false),
                _ => IpcResponse.Fail(request.Id, IpcErrorCodes.MethodNotFound, $"Metodo desconhecido: {request.Method}"),
            };
        }

        private static IpcResponse HandlePing(IpcRequest request)
        {
            JsonElement pong = JsonSerializer.SerializeToElement(new { pong = true }, IpcSerializer.Options);
            return IpcResponse.Ok(request.Id, pong);
        }

        private async Task<IpcResponse> HandleImportFamilyAsync(IpcRequest request)
        {
            ImportFamilyParams? parameters;
            try
            {
                parameters = IpcSerializer.DeserializeParams<ImportFamilyParams>(request.Params);
            }
            catch (JsonException ex)
            {
                return IpcResponse.Fail(request.Id, IpcErrorCodes.InvalidParams, "params invalido: " + ex.Message);
            }

            if (parameters == null)
            {
                return IpcResponse.Fail(request.Id, IpcErrorCodes.InvalidParams, "params ausente.");
            }

            if (string.IsNullOrWhiteSpace(parameters.DownloadUrl))
            {
                return IpcResponse.Fail(request.Id, IpcErrorCodes.InvalidParams, "downloadUrl obrigatorio.");
            }

            // Mapa Sidecar.Ipc.ImportFamilyParams -> Application.ImportFamilyRequest.
            // Mantemos as duas entidades separadas pra que a camada Application
            // continue agnostica do protocolo de IPC; o custo e este pequeno
            // copy-paste de campos.
            ImportFamilyRequest applicationRequest = new()
            {
                FamilyId = parameters.FamilyId,
                FamilyName = parameters.FamilyName ?? string.Empty,
                ManufacturerName = parameters.ManufacturerName ?? string.Empty,
                FileName = parameters.FileName ?? string.Empty,
                DownloadUrl = parameters.DownloadUrl,
                LatestVersion = parameters.LatestVersion,
                RequestedAtUtc = DateTime.UtcNow,
            };

            string localPath;
            try
            {
                localPath = await _downloadService.DownloadToCacheAsync(applicationRequest, _cacheService).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                return IpcResponse.Fail(request.Id, IpcErrorCodes.DownloadFailed, "Falha ao baixar .rfa: " + ex.Message);
            }

            // Serializa Raise + await Completed pra que duas requests concorrentes
            // do mesmo cliente nao colidam no handler (PendingRequest e mutavel).
            await _raiseLock.WaitAsync().ConfigureAwait(false);
            try
            {
                TaskCompletionSource<bool> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
                void OnCompleted() => completion.TrySetResult(true);

                _externalEvent.Completed += OnCompleted;
                try
                {
                    _externalEvent.Raise(applicationRequest, localPath);
                    await completion.Task.ConfigureAwait(false);
                }
                finally
                {
                    _externalEvent.Completed -= OnCompleted;
                }
            }
            finally
            {
                _raiseLock.Release();
            }

            ImportFamilyResult result = new()
            {
                LoadedFamilyName = applicationRequest.FamilyName,
                CachedFilePath = localPath,
            };
            JsonElement resultElement = JsonSerializer.SerializeToElement(result, IpcSerializer.Options);
            return IpcResponse.Ok(request.Id, resultElement);
        }
    }
}
