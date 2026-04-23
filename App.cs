using System;
using System.Reflection;
using Autodesk.Revit.UI;
using FamiliesImporterHub.Commands;
using FamiliesImporterHub.Infrastructure;
using FamiliesImporterHub.UI;

namespace FamiliesImporterHub
{
    public class App : IExternalApplication
    {
        private const string TabName = "TigreBIM";
        private const string PanelName = "Tigre";
        private const string ButtonName = "FamiliesImporterHub";
        private const string ButtonText = "Families\nImporter Hub";
        private const string PaneTitle = "Importar Famílias";
        private const string PipeConverterButtonName = "PipeConverter";
        private const string PipeConverterButtonText = "Converter\nCAD → Tubos";

        public Result OnStartup(UIControlledApplication application)
        {
            try
            {
                TryCreateRibbonTab(application, TabName);

                RibbonPanel panel = GetOrCreateRibbonPanel(application, TabName, PanelName);

                string assemblyPath = Assembly.GetExecutingAssembly().Location;

                PushButtonData buttonData = new PushButtonData(
                    ButtonName,
                    ButtonText,
                    assemblyPath,
                    typeof(ShowFamiliesPaneCommand).FullName!);

                PushButton? button = panel.AddItem(buttonData) as PushButton;

                if (button != null)
                {
                    button.ToolTip = "Abre o painel de importação de famílias da Tigre.";
                    button.LongDescription = "Abre um painel lateral no Revit para listar e importar famílias.";
                }

                PushButtonData pipeConverterButtonData = new PushButtonData(
                    PipeConverterButtonName,
                    PipeConverterButtonText,
                    assemblyPath,
                    typeof(ShowPipeConverterCommand).FullName!);

                PushButton? pipeConverterButton = panel.AddItem(pipeConverterButtonData) as PushButton;

                if (pipeConverterButton != null)
                {
                    pipeConverterButton.ToolTip = "Converte linhas de vínculo CAD em tubos Revit.";
                    pipeConverterButton.LongDescription =
                        "Abre uma janela para configurar sistema, tipo e diâmetro, " +
                        "e ativa um modo de seleção que converte linhas de vínculos CAD em tubos.";
                }

                FamiliesPage familiesPage = new FamiliesPage();
                DockablePaneId paneId = new DockablePaneId(PaneIds.FamiliesPaneId);

                application.RegisterDockablePane(
                    paneId,
                    PaneTitle,
                    familiesPage);

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("FamiliesImporterHub", $"Erro ao iniciar o plugin:\n{ex}");
                return Result.Failed;
            }
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            return Result.Succeeded;
        }

        private static void TryCreateRibbonTab(UIControlledApplication application, string tabName)
        {
            try
            {
                application.CreateRibbonTab(tabName);
            }
            catch
            {
            }
        }

        private static RibbonPanel GetOrCreateRibbonPanel(
            UIControlledApplication application,
            string tabName,
            string panelName)
        {
            foreach (RibbonPanel panel in application.GetRibbonPanels(tabName))
            {
                if (panel.Name.Equals(panelName, StringComparison.OrdinalIgnoreCase))
                {
                    return panel;
                }
            }

            return application.CreateRibbonPanel(tabName, panelName);
        }
    }
}