using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Pipes;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DarivaBIM.Sidecar.Ipc;

namespace DarivaBIM.FamilyBrowser.Ipc
{
    /// <summary>
    /// Cliente NamedPipe que conversa com o plugin Revit. Conecta uma vez
    /// na inicializacao, mantem a conexao viva enquanto o sidecar EXE
    /// estiver aberto, e expoe uma API request/response correlacionada por
    /// <see cref="IpcRequest.Id"/>.
    ///
    /// Protocolo wire: line-delimited JSON. Cada linha (terminada com '\n')
    /// e uma mensagem completa serializada via <see cref="IpcSerializer"/>.
    /// </summary>
    public sealed class PipeClient : IAsyncDisposable
    {
        private readonly string _pipeName;
        private NamedPipeClientStream? _pipe;
        private StreamReader? _reader;
        private StreamWriter? _writer;
        private CancellationTokenSource? _cts;
        private Task? _readLoopTask;

        // Map de requests pendentes (ID -> TaskCompletionSource). Quando uma
        // resposta chega no read loop, completa a Task aqui. Uso ConcurrentDictionary
        // porque o read loop e SendAsync rodam em threads diferentes.
        private readonly ConcurrentDictionary<string, TaskCompletionSource<IpcResponse>> _pending = new();

        // SemaphoreSlim em vez de lock pra serializar writes — async-friendly.
        // Dois SendAsync concorrentes nao podem intercalar bytes no mesmo
        // StreamWriter (corromperia o framing de linha).
        private readonly SemaphoreSlim _writeLock = new(1, 1);

        public PipeClient(string pipeName)
        {
            _pipeName = pipeName;
        }

        public bool IsConnected => _pipe?.IsConnected == true;

        public event Action<Exception>? ConnectionLost;

        /// <summary>
        /// Conecta ao pipe servidor. Timeout configuravel pra cobrir o caso de
        /// o plugin Revit ainda nao ter aberto o pipe (race de cold start).
        /// </summary>
        public async Task ConnectAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            _pipe = new NamedPipeClientStream(
                serverName: ".",
                pipeName: _pipeName,
                direction: PipeDirection.InOut,
                options: PipeOptions.Asynchronous);

            await _pipe.ConnectAsync((int)timeout.TotalMilliseconds, cancellationToken).ConfigureAwait(false);

            _reader = new StreamReader(_pipe, System.Text.Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 4096, leaveOpen: true);
            _writer = new StreamWriter(_pipe, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false), bufferSize: 4096, leaveOpen: true)
            {
                AutoFlush = true,
                NewLine = "\n",
            };

            _cts = new CancellationTokenSource();
            _readLoopTask = Task.Run(() => ReadLoopAsync(_cts.Token));
        }

        /// <summary>
        /// Envia uma requisicao e aguarda a resposta com o mesmo ID. O caller
        /// e responsavel por timeout via <paramref name="cancellationToken"/> —
        /// nao impomos default pra que operacoes potencialmente lentas
        /// (download de .rfa grande) nao caiam falsamente.
        /// </summary>
        public async Task<IpcResponse> SendAsync(IpcRequest request, CancellationToken cancellationToken = default)
        {
            if (_writer == null)
            {
                throw new InvalidOperationException("PipeClient nao esta conectado.");
            }

            if (string.IsNullOrEmpty(request.Id))
            {
                request.Id = Guid.NewGuid().ToString("N");
            }

            TaskCompletionSource<IpcResponse> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
            _pending[request.Id] = tcs;

            string json = IpcSerializer.Serialize(request);

            await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await _writer.WriteLineAsync(json).ConfigureAwait(false);
            }
            finally
            {
                _writeLock.Release();
            }

            using CancellationTokenRegistration registration = cancellationToken.Register(() =>
            {
                if (_pending.TryRemove(request.Id, out TaskCompletionSource<IpcResponse>? pending))
                {
                    pending.TrySetCanceled(cancellationToken);
                }
            });

            return await tcs.Task.ConfigureAwait(false);
        }

        private async Task ReadLoopAsync(CancellationToken cancellationToken)
        {
            StreamReader reader = _reader!;
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    string? line = await reader.ReadLineAsync().ConfigureAwait(false);
                    if (line == null)
                    {
                        // EOF: plugin fechou o pipe.
                        break;
                    }

                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    IpcResponse? response;
                    try
                    {
                        response = IpcSerializer.Deserialize<IpcResponse>(line);
                    }
                    catch (JsonException)
                    {
                        // Mensagem corrompida — ignora e segue. Em prod
                        // mereceria log; por ora, silencioso pra nao espocar.
                        continue;
                    }

                    if (response == null || string.IsNullOrEmpty(response.Id))
                    {
                        continue;
                    }

                    if (_pending.TryRemove(response.Id, out TaskCompletionSource<IpcResponse>? pending))
                    {
                        pending.TrySetResult(response);
                    }
                    // ID desconhecido: ignora (request foi cancelada antes da resposta).
                }
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                // Pipe quebrou. Notifica e completa todos os pending com erro.
                FailAllPending(ex);
                ConnectionLost?.Invoke(ex);
            }
        }

        private void FailAllPending(Exception ex)
        {
            foreach (string id in _pending.Keys)
            {
                if (_pending.TryRemove(id, out TaskCompletionSource<IpcResponse>? pending))
                {
                    pending.TrySetException(ex);
                }
            }
        }

        public async ValueTask DisposeAsync()
        {
            _cts?.Cancel();

            if (_readLoopTask != null)
            {
                try { await _readLoopTask.ConfigureAwait(false); }
                catch { /* esperado quando cancelado */ }
            }

            _writer?.Dispose();
            _reader?.Dispose();
            _pipe?.Dispose();
            _cts?.Dispose();
            _writeLock.Dispose();
        }
    }
}
