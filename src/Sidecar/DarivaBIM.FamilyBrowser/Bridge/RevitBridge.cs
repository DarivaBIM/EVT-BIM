using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DarivaBIM.FamilyBrowser.Ipc;
using DarivaBIM.Sidecar.Ipc;
using DarivaBIM.Sidecar.Ipc.Methods;

namespace DarivaBIM.FamilyBrowser.Bridge
{
    /// <summary>
    /// Dispatcher de mensagens que vem do JavaScript do AcervoBIM via
    /// <c>chrome.webview.postMessage(...)</c>. Substitui a abordagem anterior
    /// de <c>AddHostObjectToScript</c> (que dependia de COM/IDispatch e
    /// falhava com E_INVALIDARG em todas as combinacoes de assinatura nesta
    /// versao do WebView2 1.0.3967.48 + .NET 8).
    ///
    /// Esta classe e pura .NET — sem atributos COM. Cada metodo recebe o
    /// payload deserializado (ou JSON cru pro caso do importFamily, pra que
    /// o frontend possa passar campos extras sem quebrar a assinatura) e
    /// retorna uma string JSON que sera enviada de volta ao JS via
    /// <c>PostWebMessageAsJson</c>.
    /// </summary>
    public sealed class RevitBridge
    {
        private readonly PipeClient _pipe;

        public RevitBridge(PipeClient pipe)
        {
            _pipe = pipe;
        }

        /// <summary>
        /// Diagnostico minimo: ecoa o texto recebido. Existe pra validar
        /// isoladamente que o canal postMessage JS&lt;-&gt;C# funciona, sem
        /// depender de IPC, pipe ou plugin.
        /// </summary>
        public Task<string> EchoAsync(string text)
        {
            string result = IpcSerializer.Serialize(new { ok = true, text = text ?? string.Empty });
            return Task.FromResult(result);
        }

        /// <summary>
        /// Diagnostico: confirma que o pipe esta vivo e o plugin responde.
        /// Retorna JSON: <c>{ ok: true|false, message?: string }</c>.
        /// </summary>
        public async Task<string> PingAsync()
        {
            IpcRequest request = new()
            {
                Id = Guid.NewGuid().ToString("N"),
                Method = "ping",
                Params = JsonSerializer.SerializeToElement(new { }, IpcSerializer.Options),
            };

            using CancellationTokenSource cts = new(TimeSpan.FromSeconds(5));

            try
            {
                IpcResponse response = await _pipe.SendAsync(request, cts.Token).ConfigureAwait(false);
                return IpcSerializer.Serialize(new
                {
                    ok = response.Error == null,
                    message = response.Error?.Message,
                });
            }
            catch (Exception ex)
            {
                return IpcSerializer.Serialize(new { ok = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Solicita ao plugin Revit que baixe e carregue uma familia, ativando
        /// o modo placement no cursor. Recebe um JSON com os campos de
        /// <see cref="ImportFamilyParams"/>; retorna um JSON com
        /// <c>{ ok, error?, code?, message? }</c>.
        /// </summary>
        public async Task<string> ImportFamilyAsync(string paramsJson)
        {
            ImportFamilyParams? parameters;
            try
            {
                parameters = IpcSerializer.Deserialize<ImportFamilyParams>(paramsJson ?? string.Empty);
            }
            catch (JsonException ex)
            {
                return IpcSerializer.Serialize(new
                {
                    ok = false,
                    error = "bridge",
                    message = "JSON invalido: " + ex.Message,
                });
            }

            if (parameters == null)
            {
                return IpcSerializer.Serialize(new
                {
                    ok = false,
                    error = "bridge",
                    message = "JSON vazio ou ausente.",
                });
            }

            IpcRequest request = new()
            {
                Id = Guid.NewGuid().ToString("N"),
                Method = "importFamily",
                Params = JsonSerializer.SerializeToElement(parameters, IpcSerializer.Options),
            };

            using CancellationTokenSource cts = new(TimeSpan.FromMinutes(5));

            IpcResponse response;
            try
            {
                response = await _pipe.SendAsync(request, cts.Token).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                return IpcSerializer.Serialize(new
                {
                    ok = false,
                    error = "transport",
                    message = ex.Message,
                });
            }

            if (response.Error != null)
            {
                return IpcSerializer.Serialize(new
                {
                    ok = false,
                    error = "plugin",
                    code = response.Error.Code,
                    message = response.Error.Message,
                });
            }

            return IpcSerializer.Serialize(new { ok = true });
        }
    }
}
