using System;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using FamiliesImporterHub.UI;

namespace FamiliesImporterHub.Infrastructure
{
    /// <summary>
    /// Handler do <c>ExternalEvent</c> que executa UMA seleção (PickObject) por
    /// invocação. Ao final, se a ferramenta continuar ativa, ele se reagenda
    /// chamando o callback <see cref="RearmRequested"/>. Esse padrão "single-shot
    /// + re-arm" é o que permite que o usuário troque parâmetros no WPF
    /// (diâmetro, tipo, sistema, nível) sem travar o ciclo de seleção: cada
    /// pick lê os valores correntes do <c>ViewModel</c> a cada execução.
    /// </summary>
    public class PipeInsertionHandler : IExternalEventHandler
    {
        public PipeConverterViewModel? ViewModel { get; set; }

        /// <summary>
        /// Callback invocado ao final da execução para reagendar um novo pick
        /// caso a ferramenta continue ativa. Definido pelo
        /// <see cref="PipeInsertionExternalEvent"/>.
        /// </summary>
        public Action? RearmRequested { get; set; }

        public void Execute(UIApplication app)
        {
            PipeConverterViewModel? vm = ViewModel;
            if (vm == null || !vm.IsActive)
                return;

            try
            {
                UIDocument? uiDoc = app.ActiveUIDocument;
                if (uiDoc == null || uiDoc.Document.IsFamilyDocument)
                {
                    vm.IsActive = false;
                    vm.StatusMessage = "Abra um projeto Revit para usar a ferramenta.";
                    return;
                }

                Document doc = uiDoc.Document;
                CadCurveSelectionFilter filter = new();

                Reference? reference;
                try
                {
                    reference = uiDoc.Selection.PickObject(
                        ObjectType.PointOnElement,
                        filter,
                        "PipeCADMapper — clique em uma linha do CAD. Use o painel para alterar parâmetros ou desativar.");
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                {
                    // Cancelamento pode vir de:
                    //  - usuário pressionou ESC para desativar
                    //  - WPF capturou foco para alteração de parâmetros (re-armar)
                    //  - troca de view, etc.
                    // Em qualquer caso: se a ferramenta segue ativa, agenda novo pick.
                    return;
                }

                if (reference == null || !vm.IsActive)
                    return;

                // Lê parâmetros atualizados do VM a cada pick — assim mudanças
                // feitas no WPF entre uma seleção e outra entram em vigor.
                if (!TryBuildConfig(vm, out PipeConversionConfig? config, out string? configError))
                {
                    vm.StatusMessage = configError;
                    return;
                }

                PipeCreationResult result = PipeCreator.CreateFromReference(doc, reference, config!);
                vm.StatusMessage = BuildStatusMessage(vm, result);
            }
            catch (Exception ex)
            {
                vm.StatusMessage = $"Erro inesperado: {ex.Message}";
                TaskDialog.Show("TigreBIM", $"Erro na inserção de tubos:\n{ex.Message}");
            }
            finally
            {
                if (vm.IsActive)
                {
                    // Reagenda outra rodada de PickObject. O dispatch volta a
                    // passar pelo Idle do Revit, garantindo estado limpo.
                    RearmRequested?.Invoke();
                }
                else if (string.IsNullOrEmpty(vm.StatusMessage))
                {
                    vm.StatusMessage = "Ferramenta desativada.";
                }
            }
        }

        public string GetName() => "TigreBIM.PipeInsertionHandler";

        private static bool TryBuildConfig(
            PipeConverterViewModel vm,
            out PipeConversionConfig? config,
            out string? error)
        {
            config = null;
            error = null;

            PipingSystemOption? system = vm.SelectedSystem;
            PipeTypeOption? pipeType = vm.SelectedPipeType;
            double? diameter = vm.SelectedDiameterMm;
            LevelOption? level = vm.SelectedLevel;
            double offsetMm = vm.OffsetMm;

            if (system == null || pipeType == null || !diameter.HasValue || level == null)
            {
                error = "Configuração incompleta — selecione sistema, tipo, diâmetro e nível.";
                return false;
            }

            config = new PipeConversionConfig(
                system.Id,
                pipeType.Id,
                level.Id,
                diameter.Value,
                level.ElevationFeet,
                offsetMm);

            return true;
        }

        private static string BuildStatusMessage(PipeConverterViewModel vm, PipeCreationResult result)
        {
            if (!result.Success)
                return $"Não foi possível criar o tubo: {result.ErrorMessage}";

            string skippedNote = result.SkippedCount > 0
                ? $" ({result.SkippedCount} segmento(s) curto(s) ignorado(s))"
                : string.Empty;

            string arcNote = result.ArcsAsChordCount > 0
                ? $" [{result.ArcsAsChordCount} arco(s) convertido(s) como corda reta]"
                : string.Empty;

            return $"Criado(s) {result.CreatedCount} tubo(s){skippedNote}{arcNote} | " +
                   $"{vm.SelectedPipeType?.Name} Ø{vm.SelectedDiameterMm}mm | " +
                   $"{vm.SelectedLevel?.Name} + {vm.OffsetMm}mm";
        }
    }
}
