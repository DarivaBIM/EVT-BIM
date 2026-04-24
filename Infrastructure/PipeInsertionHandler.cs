using System;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using FamiliesImporterHub.UI;

namespace FamiliesImporterHub.Infrastructure
{
    public class PipeInsertionHandler : IExternalEventHandler
    {
        public PipeConverterViewModel? ViewModel { get; set; }

        public void Execute(UIApplication app)
        {
            PipeConverterViewModel? vm = ViewModel;
            if (vm == null)
            {
                return;
            }

            try
            {
                UIDocument? uiDoc = app.ActiveUIDocument;
                if (uiDoc == null || uiDoc.Document.IsFamilyDocument)
                {
                    vm.StatusMessage = "Abra um projeto Revit para usar a ferramenta.";
                    return;
                }

                Document doc = uiDoc.Document;
                CadCurveSelectionFilter filter = new CadCurveSelectionFilter();

                while (vm.IsActive)
                {
                    Reference? reference;
                    try
                    {
                        reference = uiDoc.Selection.PickObject(
                            ObjectType.PointOnElement,
                            filter,
                            "Selecione uma linha do CAD para converter em tubo (ESC para sair).");
                    }
                    catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                    {
                        break;
                    }

                    if (reference == null || !vm.IsActive)
                    {
                        break;
                    }

                    PipingSystemOption? system = vm.SelectedSystem;
                    PipeTypeOption? pipeType = vm.SelectedPipeType;
                    double? diameter = vm.SelectedDiameterMm;
                    LevelOption? level = vm.SelectedLevel;
                    double offsetMm = vm.OffsetMm;

                    if (system == null || pipeType == null || !diameter.HasValue || level == null)
                    {
                        vm.StatusMessage = "Configuração incompleta — selecione sistema, tipo, diâmetro e nível.";
                        continue;
                    }

                    PipeConversionConfig config = new PipeConversionConfig(
                        system.Id,
                        pipeType.Id,
                        level.Id,
                        diameter.Value,
                        level.ElevationFeet,
                        offsetMm);

                    PipeCreationResult result = PipeCreator.CreateFromReference(doc, reference, config);

                    if (result.Success)
                    {
                        string skippedNote = result.SkippedCount > 0
                            ? $" (ignorado(s) {result.SkippedCount} segmento(s) curto(s))"
                            : string.Empty;

                        vm.StatusMessage =
                            $"Criado(s) {result.CreatedCount} tubo(s){skippedNote} | {pipeType.Name} " +
                            $"Ø{diameter}mm | {level.Name} + {offsetMm}mm";
                    }
                    else
                    {
                        vm.StatusMessage = $"Não foi possível criar o tubo: {result.ErrorMessage}";
                    }
                }
            }
            catch (Exception ex)
            {
                TaskDialog.Show("TigreBIM", $"Erro na inserção de tubos:\n{ex.Message}");
            }
            finally
            {
                vm.IsActive = false;
                if (string.IsNullOrEmpty(vm.StatusMessage))
                {
                    vm.StatusMessage = "Ferramenta desativada.";
                }
            }
        }

        public string GetName() => "TigreBIM.PipeInsertionHandler";
    }
}
