using System;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using DarivaBIM.Plugin.Features.PipeCadMapper.Tools;
using DarivaBIM.Presentation.Wpf.PipeConverter;
using DarivaBIM.Revit.Adapters.Common.Cad;
using DarivaBIM.Revit.Adapters.Common.Filters;
using DarivaBIM.Revit.Adapters.Common.Transactions.FailurePreprocessors;
using DarivaBIM.Revit.Adapters.Features.PipeCadMapper.Markers;
using DarivaBIM.Revit.Adapters.Features.PipeCadMapper.Unifilar;

namespace DarivaBIM.Plugin.Features.PipeCadMapper
{
    /// <summary>
    /// Handler "single-shot + re-arm": cada disparo pede UM <c>PickObject</c>
    /// de linha no CAD selecionado, cria os marcadores (placeholders
    /// taggeados) para os segmentos retos daquela linha/polilinha e, se a
    /// ferramenta continuar ativa, se reagenda. Permite ao usuário trocar
    /// parâmetros (diâmetro, nível, etc.) entre cliques sem travar o ciclo
    /// — cada pick lê os valores correntes do view-model.
    /// </summary>
    public class MarkerLinePickHandler : IExternalEventHandler
    {
        public PipeConverterViewModel? ViewModel { get; set; }
        public Action? RearmRequested { get; set; }

        private int _internalCancelPending;

        public void RequestInternalCancel()
        {
            System.Threading.Interlocked.Exchange(ref _internalCancelPending, 1);
        }

        public void Execute(UIApplication app)
        {
            PipeConverterViewModel? vm = ViewModel;
            if (vm == null || !vm.IsActive) return;

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
                View activeView = doc.ActiveView;

                if (!vm.HasCadLink || string.IsNullOrEmpty(vm.SelectedLayer))
                {
                    vm.IsActive = false;
                    vm.StatusMessage = "Selecione um vínculo CAD e um layer antes de selecionar linhas.";
                    return;
                }

                Reference reference;
                try
                {
                    reference = uiDoc.Selection.PickObject(
                        ObjectType.PointOnElement,
                        new CadCurveSelectionFilter(),
                        $"PipeCADMapper — clique em uma linha do layer '{vm.SelectedLayer}'.");
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                {
                    bool internalCancel = System.Threading.Interlocked.Exchange(ref _internalCancelPending, 0) == 1;
                    if (!internalCancel)
                    {
                        vm.IsActive = false;
                        vm.StatusMessage = "Seleção de linhas encerrada.";
                    }
                    return;
                }

                if (!vm.IsActive) return;

                Element? element = doc.GetElement(reference);
                if (element is not ImportInstance importInstance)
                {
                    vm.StatusMessage = "Selecione uma linha de um vínculo CAD.";
                    return;
                }

                // Trava a ferramenta no CAD escolhido inicialmente — clicar em
                // outro vínculo durante a sessão é considerado erro de uso.
                long? selectedId = vm.SelectedCadLinkId;
                if (selectedId.HasValue && importInstance.Id.Value != selectedId.Value)
                {
                    vm.StatusMessage = "A linha clicada não pertence ao vínculo CAD selecionado.";
                    return;
                }

                GeometryObject? geom;
                string? geomError = null;
                try { geom = element.GetGeometryObjectFromReference(reference); }
                catch (Exception ex) { geom = null; geomError = ex.Message; }

                if (geom == null)
                {
                    vm.StatusMessage = geomError == null
                        ? "Geometria não disponível na referência selecionada."
                        : $"Geometria não disponível: {geomError}";
                    return;
                }

                Transform transform = CadGeometryExtractor.GetTransformForElement(element);
                UnifilarSegmentBatch? batch = UnifilarLineCollector.CollectFromReference(
                    doc, geom, transform, vm.SelectedLayer!);

                if (batch == null)
                {
                    vm.StatusMessage = $"A linha clicada não está no layer '{vm.SelectedLayer}'.";
                    return;
                }

                if (batch.Segments.Count == 0)
                {
                    vm.StatusMessage = "Geometria não suportada (apenas linhas e polylines retas viram marcadores).";
                    return;
                }

                if (!PipeMarkerConfigFactory.TryCreate(vm, out PipeMarkerConfig? config, out string? configError))
                {
                    vm.StatusMessage = configError!;
                    return;
                }

                int created;
                int skipped;
                using (Transaction tx = new(doc, "PipeCADMapper — criar marcadores (unifilar)"))
                {
                    FailureHandlingOptions failureOptions = tx.GetFailureHandlingOptions();
                    failureOptions.SetFailuresPreprocessor(new PipeCreationFailurePreprocessor());
                    failureOptions.SetClearAfterRollback(true);
                    failureOptions.SetForcedModalHandling(false);
                    tx.SetFailureHandlingOptions(failureOptions);
                    tx.Start();

                    PipeMarkerBatch result = PipeMarkerCreator.CreateFromSegments(
                        doc, activeView, batch.Segments, config!);

                    if (result.Created == 0)
                    {
                        tx.RollBack();
                        vm.StatusMessage = "Todos os segmentos eram mais curtos que a tolerância do Revit.";
                        return;
                    }

                    created = result.Created;
                    skipped = result.SkippedShort;
                    tx.Commit();
                }

                vm.ActiveViewMarkerCount = PipeMarkerCollector.CountInView(doc, activeView);
                string skipNote = skipped > 0 ? $" ({skipped} curto(s) ignorado(s))" : string.Empty;
                vm.StatusMessage = $"Criado(s) {created} marcador(es){skipNote}. Total na vista: {vm.ActiveViewMarkerCount}.";
            }
            catch (Exception ex)
            {
                vm.StatusMessage = "Erro inesperado: " + ex.Message;
            }
            finally
            {
                if (vm.IsActive) RearmRequested?.Invoke();
            }
        }

        public string GetName() => "EvtBim.MarkerLinePickHandler";
    }
}
