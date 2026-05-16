using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using DarivaBIM.Sidecar.Ipc;

namespace DarivaBIM.Plugin.Sidecar
{
    /// <summary>
    /// Singleton que gerencia o lifecycle do sidecar EXE
    /// (DarivaBIM.FamilyBrowser) e do pipe server que conversa com ele. Vive
    /// no AppDomain do Revit enquanto a sessao estiver aberta.
    ///
    /// Single-instance: o handle do processo sidecar e mantido em campo. Se
    /// <see cref="EnsureRunningAsync"/> for chamado e o sidecar ja estiver
    /// vivo, focamos a janela existente em vez de spawnar duplicata.
    /// </summary>
    public sealed class SidecarHost
    {
        private static readonly Lazy<SidecarHost> _instance = new(() => new SidecarHost());
        public static SidecarHost Instance => _instance.Value;

        // URL do AcervoBIM dentro do WebView2. Default = prod (deploy de
        // 2026-05-16 em https://acervobim.darivabim.com). Pra apontar pra
        // dev local, setar env var DARIVABIM_ACERVO_EMBED_URL antes de abrir
        // o Revit (ex.: $env:DARIVABIM_ACERVO_EMBED_URL = "http://localhost:3000/embed/revit").
        private static readonly string EmbedUrl =
            Environment.GetEnvironmentVariable("DARIVABIM_ACERVO_EMBED_URL")
            ?? "https://acervobim.darivabim.com/embed/revit";

        private readonly object _stateLock = new();
        private readonly PipeServer _pipeServer;
        private readonly ImportFamilyDispatcher _dispatcher;
        private Process? _sidecarProcess;

        private SidecarHost()
        {
            int pid = Process.GetCurrentProcess().Id;
            string pipeName = PipeNames.ForRevit(pid);

            _dispatcher = new ImportFamilyDispatcher();
            _pipeServer = new PipeServer(pipeName, _dispatcher.DispatchAsync);
        }

        /// <summary>
        /// Garante que ha um sidecar rodando. Se ja existe um processo vivo,
        /// foca a janela dele. Caso contrario, inicia o pipe server (lazy) e
        /// spawna o EXE com os args necessarios.
        ///
        /// Excecoes nao sao silenciadas — o caller (geralmente
        /// ShowFamiliesPaneCommand) decide como reportar ao usuario.
        /// </summary>
        public Task EnsureRunningAsync()
        {
            lock (_stateLock)
            {
                if (_sidecarProcess != null && !_sidecarProcess.HasExited)
                {
                    TryFocusExistingWindow(_sidecarProcess);
                    return Task.CompletedTask;
                }

                _sidecarProcess = null;

                if (!_pipeServer.IsRunning)
                {
                    _pipeServer.Start();
                }

                string exePath = LocateSidecarExecutable();
                Process process = SpawnSidecar(exePath, _pipeServer.PipeName);
                _sidecarProcess = process;
            }

            return Task.CompletedTask;
        }

        // Caminho default do sidecar: subpasta "Sidecar" junto do plugin
        // (copiada pelo build target nos csprojs V2025/V2026). Em dev, esse
        // diretorio fica em %ProgramData%\Autodesk\Revit\Addins\<ano>\EVT-BIM\Sidecar\.
        private static string LocateSidecarExecutable()
        {
            string pluginDir = Path.GetDirectoryName(typeof(SidecarHost).Assembly.Location)
                ?? AppContext.BaseDirectory;
            string sidecarPath = Path.Combine(pluginDir, "Sidecar", "DarivaBIM.FamilyBrowser.exe");

            if (!File.Exists(sidecarPath))
            {
                throw new FileNotFoundException(
                    "Sidecar EXE nao encontrado. Espera-se que o build do plugin tenha copiado pra subpasta Sidecar/.",
                    sidecarPath);
            }

            return sidecarPath;
        }

        private static Process SpawnSidecar(string exePath, string pipeName)
        {
            ProcessStartInfo psi = new()
            {
                FileName = exePath,
                UseShellExecute = false,
                CreateNoWindow = false,
                WorkingDirectory = Path.GetDirectoryName(exePath) ?? string.Empty,
            };
            psi.ArgumentList.Add("--pipe");
            psi.ArgumentList.Add(pipeName);
            psi.ArgumentList.Add("--url");
            psi.ArgumentList.Add(EmbedUrl);

            Process? process = Process.Start(psi)
                ?? throw new InvalidOperationException("Process.Start retornou null para o sidecar.");

            return process;
        }

        private static void TryFocusExistingWindow(Process process)
        {
            // Process.MainWindowHandle pode estar zerado se o processo ainda
            // esta inicializando — nesse caso o foco vai ser dado naturalmente
            // pelo proprio Show() do WPF no sidecar. Sem retry aqui.
            process.Refresh();
            IntPtr hwnd = process.MainWindowHandle;
            if (hwnd == IntPtr.Zero)
            {
                return;
            }

            const int SW_RESTORE = 9;
            ShowWindow(hwnd, SW_RESTORE);
            SetForegroundWindow(hwnd);
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        /// <summary>
        /// Encerra pipe server e mata o sidecar. Chamado no shutdown do plugin
        /// (IExternalApplication.OnShutdown) pra liberar o pipe e nao deixar
        /// orfaos rodando depois de fechar o Revit.
        /// </summary>
        public void Shutdown()
        {
            lock (_stateLock)
            {
                _pipeServer.Dispose();

                if (_sidecarProcess != null && !_sidecarProcess.HasExited)
                {
                    try
                    {
                        _sidecarProcess.CloseMainWindow();
                        if (!_sidecarProcess.WaitForExit(2000))
                        {
                            _sidecarProcess.Kill();
                        }
                    }
                    catch
                    {
                        // Best-effort: se nao consegue matar, sistema operacional
                        // vai limpar quando o user logout/reiniciar.
                    }
                }
                _sidecarProcess?.Dispose();
                _sidecarProcess = null;
            }
        }
    }
}
