using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using DarivaBIM.FamilyBrowser.Bridge;
using DarivaBIM.FamilyBrowser.Ipc;
using DarivaBIM.Sidecar.Ipc;
using Microsoft.Web.WebView2.Core;

namespace DarivaBIM.FamilyBrowser
{
    /// <summary>
    /// Janela principal do sidecar. Hospeda um unico controle WebView2 que
    /// renderiza o AcervoBIM. Toda comunicacao JS&lt;-&gt;C# vai por
    /// <c>chrome.webview.postMessage</c> + <see cref="CoreWebView2.WebMessageReceived"/>
    /// (NAO via AddHostObjectToScript/COM — esse caminho falha com E_INVALIDARG
    /// nesta combinacao de WebView2 + .NET 8, regardless da assinatura).
    /// </summary>
    public partial class MainWindow : Window
    {
        // Script injetado em toda pagina carregada pelo WebView2. Expoe
        // window.revit com helpers que envolvem postMessage + correlacao
        // de respostas por id. Frontend (AcervoBIM) so precisa fazer:
        //   const result = await window.revit.importFamily({...});
        // sem se preocupar com o protocolo de mensagens.
        private const string BridgeScript = @"
(() => {
  if (window.revit) return;
  const pending = new Map();
  chrome.webview.addEventListener('message', (e) => {
    const msg = e.data;
    if (msg && msg.id && pending.has(msg.id)) {
      const { resolve } = pending.get(msg.id);
      pending.delete(msg.id);
      resolve(msg.result);
    }
  });
  function call(method, params) {
    return new Promise((resolve, reject) => {
      const id = (window.crypto && crypto.randomUUID && crypto.randomUUID())
                 || (Date.now() + '-' + Math.random());
      pending.set(id, { resolve, reject });
      chrome.webview.postMessage({ id, method, params: params || {} });
      setTimeout(() => {
        if (pending.has(id)) {
          pending.delete(id);
          reject(new Error('Revit timeout: ' + method));
        }
      }, 300000);
    });
  }
  window.revit = {
    call,
    echo: (text) => call('echo', { text }),
    ping: () => call('ping', {}),
    importFamily: (params) => call('importFamily', params || {})
  };
  window.dispatchEvent(new CustomEvent('revit:ready'));
})();
";

        private readonly string _initialUrl;
        private readonly string? _pipeName;
        private PipeClient? _pipeClient;
        private RevitBridge? _revitBridge;

        public MainWindow(string initialUrl, string? pipeName)
        {
            _initialUrl = initialUrl;
            _pipeName = pipeName;
            InitializeComponent();
            StatusUrl.Text = initialUrl;
            StatusPipe.Text = pipeName != null
                ? "Pipe: conectando…"
                : "Pipe: — (sem plugin)";
            DebugButton.IsEnabled = pipeName != null;
            Loaded += OnLoaded;
            Closed += OnClosed;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!Uri.TryCreate(_initialUrl, UriKind.Absolute, out Uri? targetUri))
                {
                    ShowFatalError(
                        $"URL invalida: \"{_initialUrl}\".\n\n" +
                        "Forneca uma URL absoluta via --url <url> ao iniciar o EXE.");
                    return;
                }

                string userDataFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "DarivaBIM",
                    "FamilyBrowser",
                    "WebView2Cache");
                Directory.CreateDirectory(userDataFolder);

                CoreWebView2Environment environment =
                    await CoreWebView2Environment.CreateAsync(null, userDataFolder);

                await WebView.EnsureCoreWebView2Async(environment);

                WebView.CoreWebView2.NavigationStarting += (_, args) =>
                {
                    StatusState.Text = "Carregando…";
                    StatusUrl.Text = args.Uri;
                };
                WebView.CoreWebView2.NavigationCompleted += (_, args) =>
                {
                    StatusState.Text = args.IsSuccess
                        ? "OK"
                        : $"Erro ({args.WebErrorStatus})";
                };

                // Injeta o helper window.revit em toda pagina carregada,
                // ANTES de qualquer script da pagina rodar. Sem isso, o
                // frontend que checasse "window.revit existe?" no boot
                // racearia com a injecao.
                await WebView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(BridgeScript);

                // Hook do canal de IPC pelo qual o JS conversa com este EXE.
                WebView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;

                // Conecta no pipe do plugin Revit (best-effort).
                await ConnectPipeAsync();

                WebView.Source = targetUri;
            }
            catch (Exception ex)
            {
                ShowFatalError(
                    "Falha ao inicializar WebView2.\n\n" +
                    "Provavelmente o runtime WebView2 nao esta instalado nesta maquina.\n" +
                    "Instalador: https://developer.microsoft.com/microsoft-edge/webview2/\n\n" +
                    "Detalhes tecnicos:\n" + ex.Message);
            }
        }

        private async Task ConnectPipeAsync()
        {
            if (_pipeName == null)
            {
                return;
            }

            try
            {
                _pipeClient = new PipeClient(_pipeName);
                await _pipeClient.ConnectAsync(TimeSpan.FromSeconds(10));
                _pipeClient.ConnectionLost += OnPipeConnectionLost;
                _revitBridge = new RevitBridge(_pipeClient);
                StatusPipe.Text = "Pipe: conectado";
            }
            catch (Exception ex)
            {
                StatusPipe.Text = $"Pipe: falhou ({ex.GetType().Name})";
            }
        }

        private void OnPipeConnectionLost(Exception ex)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                StatusPipe.Text = "Pipe: desconectado";
            }));
        }

        private async void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            string requestJson;
            try
            {
                requestJson = e.WebMessageAsJson;
            }
            catch
            {
                return;
            }

            string id = string.Empty;
            string method = string.Empty;
            JsonElement paramsElement = default;

            try
            {
                using JsonDocument doc = JsonDocument.Parse(requestJson);
                JsonElement root = doc.RootElement;
                if (root.TryGetProperty("id", out JsonElement idElement))
                {
                    id = idElement.GetString() ?? string.Empty;
                }
                if (root.TryGetProperty("method", out JsonElement methodElement))
                {
                    method = methodElement.GetString() ?? string.Empty;
                }
                if (root.TryGetProperty("params", out JsonElement p))
                {
                    paramsElement = p.Clone();
                }
            }
            catch (Exception ex)
            {
                await PostResponseAsync(id, IpcSerializer.Serialize(new
                {
                    ok = false,
                    error = "bridge",
                    message = "JSON invalido: " + ex.Message,
                }));
                return;
            }

            string resultJson = await DispatchAsync(method, paramsElement).ConfigureAwait(false);
            await PostResponseAsync(id, resultJson);
        }

        private Task<string> DispatchAsync(string method, JsonElement paramsElement)
        {
            switch (method)
            {
                case "echo":
                    string text = paramsElement.ValueKind == JsonValueKind.Object &&
                                  paramsElement.TryGetProperty("text", out JsonElement textEl)
                        ? textEl.GetString() ?? string.Empty
                        : string.Empty;
                    return _revitBridge != null
                        ? _revitBridge.EchoAsync(text)
                        : Task.FromResult(IpcSerializer.Serialize(new { ok = true, text }));

                case "ping":
                    return _revitBridge != null
                        ? _revitBridge.PingAsync()
                        : Task.FromResult(IpcSerializer.Serialize(new { ok = false, message = "Pipe nao conectado." }));

                case "importFamily":
                    if (_revitBridge == null)
                    {
                        return Task.FromResult(IpcSerializer.Serialize(new
                        {
                            ok = false,
                            error = "bridge",
                            message = "Pipe nao conectado.",
                        }));
                    }
                    string paramsJson = paramsElement.ValueKind == JsonValueKind.Undefined
                        ? "{}"
                        : paramsElement.GetRawText();
                    return _revitBridge.ImportFamilyAsync(paramsJson);

                default:
                    return Task.FromResult(IpcSerializer.Serialize(new
                    {
                        ok = false,
                        error = "bridge",
                        message = "Metodo desconhecido: " + method,
                    }));
            }
        }

        // Envia a resposta de volta pro JS. PostWebMessageAsJson precisa ser
        // chamado na UI thread (dona do controle WebView2), por isso o
        // Dispatcher.InvokeAsync. O JSON inteiro vira o e.data do listener
        // de 'message' no JS.
        private async Task PostResponseAsync(string id, string resultJson)
        {
            try
            {
                using JsonDocument resultDoc = JsonDocument.Parse(resultJson);
                string envelope = JsonSerializer.Serialize(new
                {
                    id,
                    result = resultDoc.RootElement,
                }, IpcSerializer.Options);

                await Dispatcher.InvokeAsync(() =>
                {
                    WebView.CoreWebView2?.PostWebMessageAsJson(envelope);
                });
            }
            catch
            {
                // Best-effort — se o WebView2 ja foi disposed durante uma
                // request pendente, nada a fazer.
            }
        }

        private async void OnDebugClick(object sender, RoutedEventArgs e)
        {
            if (_revitBridge == null)
            {
                MessageBox.Show(
                    this,
                    "Bridge nao inicializado. Pipe esta conectado?",
                    "Teste IPC",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            DebugButton.IsEnabled = false;
            try
            {
                // Familia generica com URL invalida (example.invalid) — o
                // teste valida que IPC + dispatcher funcionam ate o ponto do
                // download falhar com erro amigavel.
                string paramsJson =
                    "{\"familyId\":1," +
                    "\"familyName\":\"Familia de teste\"," +
                    "\"manufacturerName\":\"DarivaBIM\"," +
                    "\"fileName\":\"teste.rfa\"," +
                    "\"downloadUrl\":\"https://example.invalid/teste.rfa\"," +
                    "\"latestVersion\":1}";

                string result = await _revitBridge.ImportFamilyAsync(paramsJson);

                MessageBox.Show(
                    this,
                    "Resposta do plugin:\n\n" + result,
                    "Teste IPC",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    "Erro na chamada IPC:\n\n" + ex.Message,
                    "Teste IPC",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                DebugButton.IsEnabled = true;
            }
        }

        private async void OnClosed(object? sender, EventArgs e)
        {
            if (_pipeClient != null)
            {
                await _pipeClient.DisposeAsync();
                _pipeClient = null;
            }
        }

        private void ShowFatalError(string message)
        {
            MessageBox.Show(
                this,
                message,
                "DarivaBIM FamilyBrowser",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Close();
        }
    }
}
