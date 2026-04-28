using System;
using System.Reflection;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
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
        private const string PipeConverterButtonText = "PipeCADMapper";
        private const string TigreCodesButtonName = "TigreCodes";
        private const string TigreCodesButtonText = "Códigos\nTigre";
        private const string ProlongadorButtonName = "Prolongador";
        private const string ProlongadorButtonText = "Prolongador\nem caixas";
        private const string ParameterEditorButtonName = "ParameterEditor";
        private const string ParameterEditorButtonText = "Editor de\nParâmetros";

        private Document? _lastActiveDocument;

        public Result OnStartup(UIControlledApplication application)
        {
            try
            {
                application.ViewActivated += OnViewActivated;

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
                    pipeConverterButton.ToolTip = "PipeCADMapper — converte linhas de vínculo CAD em tubos Revit com conexões automáticas.";
                    pipeConverterButton.LongDescription =
                        "Abre a janela PipeCADMapper para configurar sistema, tipo e diâmetro. " +
                        "O modo de seleção converte linhas de vínculos CAD em tubos e conecta " +
                        "automaticamente segmentos adjacentes e tubos existentes.";
                }

                PushButtonData tigreCodesButtonData = new PushButtonData(
                    TigreCodesButtonName,
                    TigreCodesButtonText,
                    assemblyPath,
                    typeof(ApplyTigreCodesCommand).FullName!);

                PushButton? tigreCodesButton = panel.AddItem(tigreCodesButtonData) as PushButton;

                if (tigreCodesButton != null)
                {
                    tigreCodesButton.ToolTip = "Atribui o parâmetro 'Tigre: Código' a cada tubo conforme descrição/segmento e diâmetro.";
                    tigreCodesButton.LongDescription =
                        "Garante o shared parameter 'Tigre: Código' como instância na categoria " +
                        "Tubulações e percorre todos os tubos do projeto preenchendo o código " +
                        "Tigre correspondente, com base no catálogo embutido (descrição + diâmetro).";
                }

                PushButtonData prolongadorButtonData = new PushButtonData(
                    ProlongadorButtonName,
                    ProlongadorButtonText,
                    assemblyPath,
                    typeof(ShowProlongadorCommand).FullName!);

                PushButton? prolongadorButton = panel.AddItem(prolongadorButtonData) as PushButton;

                if (prolongadorButton != null)
                {
                    prolongadorButton.ToolTip = "Cria prolongadores (tubos verticais) acima de caixas sifonadas/secas.";
                    prolongadorButton.LongDescription =
                        "Abre uma janela para informar o comprimento (em metros) e selecionar " +
                        "as caixas no projeto. Para cada caixa, busca o conector vertical e cria " +
                        "um tubo vertical com diâmetro herdado do conector e tipo coerente com o " +
                        "material (Redux/Reforçada/Série Normal).";
                }

                PushButtonData parameterEditorButtonData = new PushButtonData(
                    ParameterEditorButtonName,
                    ParameterEditorButtonText,
                    assemblyPath,
                    typeof(ShowParameterEditorCommand).FullName!);

                PushButton? parameterEditorButton = panel.AddItem(parameterEditorButtonData) as PushButton;

                if (parameterEditorButton != null)
                {
                    parameterEditorButton.ToolTip =
                        "Atribui um valor a um parâmetro comum a vários elementos (incluindo famílias aninhadas).";
                    parameterEditorButton.LongDescription =
                        "Abre uma janela onde o usuário seleciona elementos do projeto (com " +
                        "seleção incremental — Ctrl/Shift+clique e múltiplas rodadas de seleção), " +
                        "escolhe um parâmetro comum a todos em um dropdown e informa o valor a " +
                        "ser atribuído. O valor é propagado para o elemento selecionado e para " +
                        "as famílias aninhadas que tiverem o mesmo parâmetro. A janela mostra o " +
                        "tipo do parâmetro (texto, número inteiro, decimal etc.) para evitar " +
                        "valores incompatíveis e exibe um resumo de sucesso/falhas ao final.";
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
            application.ViewActivated -= OnViewActivated;
            return Result.Succeeded;
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