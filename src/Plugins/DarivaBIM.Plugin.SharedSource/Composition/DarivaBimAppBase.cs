using System;
using System.Reflection;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using DarivaBIM.Plugin.Ribbon;
using DarivaBIM.Plugin.Sidecar;
using DarivaBIM.Plugin.Ui;
using DarivaBIM.Revit.Abstractions.Ribbon;
using DarivaBIM.Revit.Hosting.Commands;
using DarivaBIM.Revit.Hosting.DependencyInjection;
using DarivaBIM.Revit.Hosting.Ribbon;
using Microsoft.Extensions.DependencyInjection;

namespace DarivaBIM.Plugin.Composition
{
    /// <summary>
    /// Base de <see cref="IExternalApplication"/> compartilhada entre
    /// Plugin.V2025 e Plugin.V2026. Cada plugin mantém uma classe <c>App</c>
    /// final que herda daqui apenas para fornecer o adapter Revit-API
    /// específico via <see cref="AddRevitAdapters"/>. A classe concreta tem
    /// que viver em namespace versionada porque o manifesto <c>.addin</c>
    /// pino o <c>FullClassName</c> em <c>DarivaBIM.Plugin.V2025.App</c> /
    /// <c>V2026.App</c>; tudo o mais — DI, ribbon,
    /// <c>ViewActivated</c> — é idêntico nas duas versões e mora aqui.
    /// </summary>
    public abstract class DarivaBimAppBase : IExternalApplication
    {
        private PluginHost? _host;
        private Document? _lastActiveDocument;

        public Result OnStartup(UIControlledApplication application)
        {
            try
            {
                _host = BuildHost(application);

                // Construtor do executor seta RevitCommandExecutor.Current,
                // que é como os IExternalCommands em Plugin.SharedSource
                // resolvem o escopo de DI por comando.
                _ = new RevitCommandExecutor(_host);

                ICommandRegistry registry = _host.Root.GetRequiredService<ICommandRegistry>();
                string assemblyPath = Assembly.GetExecutingAssembly().Location;

                var ribbonBuilder = new RibbonBuilder(registry, assemblyPath);
                ribbonBuilder.Build(application, DarivaBimRibbonDefinition.Build());

                // A janela de Importar Famílias é criada sob demanda pelo
                // ShowFamiliesPaneCommand quando o usuário clicar no botão
                // da ribbon. Migrado de DockablePane (regressão do Revit
                // 2025+ que congelava a pane após placement) para Window
                // modeless — eliminando o wrapper de docking quebrado e
                // economizando o custo de instanciar a UI no startup.

                application.ViewActivated += OnViewActivated;

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("EVT-BIM", $"Erro ao iniciar o plugin:\n{ex}");
                return Result.Failed;
            }
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            application.ViewActivated -= OnViewActivated;

            // Encerra sidecar EXE + pipe server. Sem isso, o usuario fica com
            // o navegador de familias aberto depois de fechar o Revit (pipe
            // morto, sem como reconectar) ate matar manualmente no Task
            // Manager. Shutdown e best-effort: nao propaga excecoes pra nao
            // mascarar erros maiores no fechamento do plugin.
            try { SidecarHost.Instance.Shutdown(); }
            catch { /* nada util a fazer no shutdown */ }

            _host?.Dispose();
            _host = null;
            return Result.Succeeded;
        }

        /// <summary>
        /// Registra os adapters Revit-versão-específicos do plugin
        /// (<c>AddRevitAdaptersV2025</c> ou <c>AddRevitAdaptersV2026</c>).
        /// </summary>
        protected abstract IServiceCollection AddRevitAdapters(IServiceCollection services);

        private PluginHost BuildHost(UIControlledApplication application)
        {
            var services = new ServiceCollection();

            services
                .AddPluginShared(application)
                .AddDarivaApplication()
                .AddDarivaInfrastructure();

            AddRevitAdapters(services);

            services
                .AddDarivaPresentation()
                .AddPluginFeatures();

            return new PluginHost(services);
        }

        private void OnViewActivated(object? sender, ViewActivatedEventArgs e)
        {
            // ViewActivated dispara em qualquer troca de view; só recarrega
            // quando o documento ativo muda de fato.
            Document? newDocument = e.Document;
            if (ReferenceEquals(newDocument, _lastActiveDocument))
            {
                return;
            }

            _lastActiveDocument = newDocument;
            PipeConverterWindow.RequestDataReload();
        }
    }
}
