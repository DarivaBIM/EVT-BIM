using System;
using System.Reflection;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using DarivaBIM.Plugin.V2026.Composition;
using DarivaBIM.Plugin.V2026.Ribbon;
using DarivaBIM.Plugin.V2026.Ui;
using DarivaBIM.Revit.Abstractions.Ribbon;
using DarivaBIM.Revit.Hosting.Commands;
using DarivaBIM.Revit.Hosting.DependencyInjection;
using DarivaBIM.Revit.Hosting.Ribbon;
using Microsoft.Extensions.DependencyInjection;

namespace DarivaBIM.Plugin.V2026
{
    public class App : IExternalApplication
    {
        private const string PaneTitle = "Importar Famílias";

        // Process-wide singletons set during OnStartup. Commands access them
        // via the static <see cref="Executor"/>.
        private static PluginHost? _host;
        public static RevitCommandExecutor Executor { get; private set; } = null!;

        private Document? _lastActiveDocument;

        public Result OnStartup(UIControlledApplication application)
        {
            try
            {
                _host = BuildHost(application);
                Executor = new RevitCommandExecutor(_host);

                ICommandRegistry registry = (ICommandRegistry)_host.Root.GetService(typeof(ICommandRegistry))!;
                string assemblyPath = Assembly.GetExecutingAssembly().Location;

                var ribbonBuilder = new RibbonBuilder(registry, assemblyPath);
                ribbonBuilder.Build(application, DarivaBimRibbonDefinition.Build());

                FamiliesPage familiesPage = new FamiliesPage();
                DockablePaneId paneId = new DockablePaneId(PaneIds.FamiliesPaneId);
                application.RegisterDockablePane(paneId, PaneTitle, familiesPage);

                application.ViewActivated += OnViewActivated;

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("DarivaBIM", $"Erro ao iniciar o plugin:\n{ex}");
                return Result.Failed;
            }
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            application.ViewActivated -= OnViewActivated;
            _host?.Dispose();
            _host = null;
            return Result.Succeeded;
        }

        private static PluginHost BuildHost(UIControlledApplication application)
        {
            var services = new ServiceCollection();

            services
                .AddPluginV2026(application)
                .AddDarivaApplication()
                .AddDarivaInfrastructure()
                .AddRevitAdaptersV2026()
                .AddDarivaPresentation()
                .AddPluginFeaturesV2026();

            return new PluginHost(services);
        }

        private void OnViewActivated(object sender, ViewActivatedEventArgs e)
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
