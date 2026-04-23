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

                    string curveKind = DescribeReference(doc, reference);

                    // Passo 4: criar o tubo real aqui com Pipe.Create.
                    vm.StatusMessage =
                        $"Selecionado: {curveKind} | {system.Name} | {pipeType.Name} | " +
                        $"Ø{diameter}mm | {level.Name} + {offsetMm}mm";
                }
            }
            catch (Exception ex)
            {
                TaskDialog.Show("TigreBIM", $"Erro na inserção de tubos:\n{ex.Message}");
            }
            finally
            {
                vm.IsActive = false;
                if (string.IsNullOrEmpty(vm.StatusMessage) ||
                    vm.StatusMessage.StartsWith("Selecionado:"))
                {
                    vm.StatusMessage = "Ferramenta desativada.";
                }
            }
        }

        public string GetName() => "TigreBIM.PipeInsertionHandler";

        private static string DescribeReference(Document doc, Reference reference)
        {
            try
            {
                Element element = doc.GetElement(reference);
                GeometryObject? geom = element?.GetGeometryObjectFromReference(reference);

                return geom switch
                {
                    Line _ => "Linha",
                    Arc _ => "Arco",
                    PolyLine _ => "Polyline",
                    _ => geom?.GetType().Name ?? "Geometria"
                };
            }
            catch
            {
                return "Geometria";
            }
        }
    }
}
