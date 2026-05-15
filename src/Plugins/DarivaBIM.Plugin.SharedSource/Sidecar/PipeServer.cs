using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DarivaBIM.Sidecar.Ipc;

namespace DarivaBIM.Plugin.Sidecar
{
    /// <summary>
    /// NamedPipe server que aceita conexao do sidecar EXE e processa mensagens
    /// no protocolo line-delimited JSON definido em <see cref="DarivaBIM.Sidecar.Ipc"/>.
    ///
    /// Modelo single-client: aceita uma conexao por vez. Quando o sidecar
    /// desconecta (usuario fechou a janela), o server volta a esperar pela
    /// proxima conexao. Esse padrao casa com o lifecycle "1 plugin <-> 1
    /// sidecar" — nao temos cenario de multiplas conexoes simultaneas.
    ///
    /// As mensagens sao dispatchadas via <see cref="MessageDispatcher"/>;
    /// o server em si nao conhece nada do dominio.
    /// </summary>
    public sealed class PipeServer : IDisposable
    {
        private readonly string _pipeName;
        private readonly Func<IpcRequest, Task<IpcResponse>> _dispatcher;
        private CancellationTokenSource? _cts;
        private Task? _acceptLoopTask;

        public PipeServer(string pipeName, Func<IpcRequest, Task<IpcResponse>> dispatcher)
        {
            _pipeName = pipeName;
            _dispatcher = dispatcher;
        }

        public string PipeName => _pipeName;

        public bool IsRunning => _acceptLoopTask != null && !_acceptLoopTask.IsCompleted;

        public void Start()
        {
            if (_acceptLoopTask != null)
            {
                return;
            }

            _cts = new CancellationTokenSource();
            _acceptLoopTask = Task.Run(() => AcceptLoopAsync(_cts.Token));
        }

        public void Stop()
        {
            _cts?.Cancel();
        }

        private async Task AcceptLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                NamedPipeServerStream? pipe = null;
                try
                {
                    // PipeDirection.InOut: bidirecional (server le requests e
                    // escreve responses na mesma conexao).
                    // maxNumberOfServerInstances=1: garante que apenas uma
                    // instancia do server escute esse nome; outra tentativa
                    // de criar falharia. Combina com nosso lifecycle de uma
                    // sidecar por Revit.
                    pipe = new NamedPipeServerStream(
                        _pipeName,
                        PipeDirection.InOut,
                        maxNumberOfServerInstances: 1,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous);

                    await pipe.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);

                    await HandleClientAsync(pipe, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                    // Conexao quebrou (cliente morreu, leitura falhou). Loop
                    // continua e reabre o server pra proxima conexao.
                }
                finally
                {
                    pipe?.Dispose();
                }
            }
        }

        private async Task HandleClientAsync(NamedPipeServerStream pipe, CancellationToken cancellationToken)
        {
            // Reader e writer com leaveOpen=true pra que o using do pipe seja
            // o unico dono do stream.
            using StreamReader reader = new(pipe, new UTF8Encoding(false), false, 4096, leaveOpen: true);
            using StreamWriter writer = new(pipe, new UTF8Encoding(false), 4096, leaveOpen: true)
            {
                AutoFlush = true,
                NewLine = "\n",
            };

            // SemaphoreSlim serializa escritas; em principio so o loop de
            // recebimento escreve, mas se no futuro o server iniciar tambem
            // server-push, o lock ja esta aqui.
            SemaphoreSlim writeLock = new(1, 1);

            while (!cancellationToken.IsCancellationRequested && pipe.IsConnected)
            {
                string? line;
                try
                {
                    line = await reader.ReadLineAsync().ConfigureAwait(false);
                }
                catch (IOException)
                {
                    break;
                }

                if (line == null)
                {
                    break;
                }

                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                // Cada mensagem e processada em fire-and-forget na thread pool
                // pra que requests caras (download de .rfa grande) nao bloqueiem
                // a leitura da proxima request. A correlacao response/request
                // e via IpcResponse.Id que o cliente ja sabe esperar.
                _ = Task.Run(async () =>
                {
                    IpcResponse response = await ProcessLineAsync(line).ConfigureAwait(false);

                    await writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
                    try
                    {
                        string responseJson = IpcSerializer.Serialize(response);
                        await writer.WriteLineAsync(responseJson).ConfigureAwait(false);
                    }
                    catch
                    {
                        // Pipe quebrou no meio do write. Nada util a fazer.
                    }
                    finally
                    {
                        writeLock.Release();
                    }
                });
            }
        }

        private async Task<IpcResponse> ProcessLineAsync(string line)
        {
            IpcRequest? request;
            try
            {
                request = IpcSerializer.Deserialize<IpcRequest>(line);
            }
            catch (JsonException ex)
            {
                return IpcResponse.Fail(string.Empty, IpcErrorCodes.ParseError, ex.Message);
            }

            if (request == null || string.IsNullOrEmpty(request.Method))
            {
                return IpcResponse.Fail(request?.Id ?? string.Empty, IpcErrorCodes.InvalidRequest, "Request sem method.");
            }

            try
            {
                return await _dispatcher(request).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                return IpcResponse.Fail(request.Id, IpcErrorCodes.InternalError, ex.Message);
            }
        }

        public void Dispose()
        {
            Stop();
            try
            {
                _acceptLoopTask?.Wait(TimeSpan.FromSeconds(2));
            }
            catch
            {
                // Suprime — pode jogar AggregateException com OperationCanceledException.
            }
            _cts?.Dispose();
        }
    }
}
