using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using DarivaBIM.Plugin.Features.PipeCadMapper.Tools;
using DarivaBIM.Presentation.Wpf.PipeConverter;
using DarivaBIM.Revit.Adapters.Common.Cad;
using DarivaBIM.Revit.Adapters.Common.Filters;
using DarivaBIM.Revit.Adapters.Common.Transactions.FailurePreprocessors;
using DarivaBIM.Revit.Adapters.Features.PipeCadMapper.Bifilar;
using DarivaBIM.Revit.Adapters.Features.PipeCadMapper.Markers;
using DarivaBIM.Revit.Adapters.Features.PipeCadMapper.Unifilar;

namespace DarivaBIM.Plugin.Features.PipeCadMapper
{
    /// <summary>
    /// Picker bifilar. O usuário clica em UMA linha do layer (uma das paredes
    /// do tubo) e o handler:
    /// 1) roda o detector restrito a essa linha como âncora;
    /// 2) acha a parede paralela compatível (mesma janela de distância do tipo
    ///    de tubo, overlap suficiente);
    /// 3) cria um marcador no eixo médio, com diâmetro derivado da distância
    ///    entre as paredes (snappado para a lista do tipo).
    /// Se não houver paralela compatível, exibe aviso e o loop continua para
    /// o próximo pick — o usuário aborta com ESC, igual ao unifilar.
    /// </summary>
    public class BifilarMarkerLinePickHandler : IExternalEventHandler
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
                        $"PipeCADMapper bifilar — clique em uma das paredes do tubo (layer '{vm.SelectedLayer}').");
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                {
                    bool internalCancel = System.Threading.Interlocked.Exchange(ref _internalCancelPending, 0) == 1;
                    if (!internalCancel)
                    {
                        vm.IsActive = false;
                        vm.StatusMessage = "Seleção bifilar encerrada.";
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

                long? selectedId = vm.SelectedCadLinkId;
                if (selectedId.HasValue && importInstance.Id.Value != selectedId.Value)
                {
                    vm.StatusMessage = "A linha clicada não pertence ao vínculo CAD selecionado.";
                    return;
                }

                GeometryObject? geom;
                try { geom = element.GetGeometryObjectFromReference(reference); }
                catch { geom = null; }

                if (geom == null)
                {
                    vm.StatusMessage = "Geometria não disponível na referência selecionada.";
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
                    vm.StatusMessage = "Geometria não suportada (apenas linhas e polylines retas).";
                    return;
                }

                // Polilinhas viram vários segmentos: usa o mais longo como
                // âncora. Geralmente é o que o usuário quis selecionar; o
                // detector ainda fará coalesce + pareamento por proximidade
                // ao midpoint, então uma escolha aproximada já casa o
                // candidate certo.
                (XYZ anchorStart, XYZ anchorEnd) = batch.Segments[0];
                double bestLen = anchorStart.DistanceTo(anchorEnd);
                for (int i = 1; i < batch.Segments.Count; i++)
                {
                    double len = batch.Segments[i].Start.DistanceTo(batch.Segments[i].End);
                    if (len > bestLen)
                    {
                        bestLen = len;
                        (anchorStart, anchorEnd) = batch.Segments[i];
                    }
                }

                IReadOnlyList<double> availableDiameters =
                    vm.SelectedPipeType?.AvailableDiametersMm ?? Array.Empty<double>();

                BifilarDetectionParameters parameters = BifilarDetectionParameters.FromTolerance(
                    vm.TolerancePercent, availableDiameters);

                BifilarCenterlineDetector detector = new(doc, parameters);
                BifilarCenterline? centerline = detector.DetectForAnchor(
                    importInstance, vm.SelectedLayer!, anchorStart, anchorEnd);

                if (centerline == null)
                {
                    vm.StatusMessage = "Nenhuma parede paralela compatível encontrada. Ajuste a tolerância ou pegue uma linha mais limpa.";
                    return;
                }

                if (!PipeMarkerConfigFactory.TryCreate(vm, out PipeMarkerConfig? config, out string? configError))
                {
                    vm.StatusMessage = configError!;
                    return;
                }

                int created;
                int skipped;
                using (Transaction tx = new(doc, "PipeCADMapper — marcador bifilar (pick)"))
                {
                    FailureHandlingOptions failureOptions = tx.GetFailureHandlingOptions();
                    failureOptions.SetFailuresPreprocessor(new PipeCreationFailurePreprocessor());
                    failureOptions.SetClearAfterRollback(true);
                    failureOptions.SetForcedModalHandling(false);
                    tx.SetFailureHandlingOptions(failureOptions);
                    tx.Start();

                    PipeMarkerBatch result = PipeMarkerCreator.CreateFromCenterlines(
                        doc, activeView, new[] { centerline }, config!, availableDiameters);

                    if (result.Created == 0)
                    {
                        tx.RollBack();
                        vm.StatusMessage = "Eixo detectado era curto demais para virar marcador.";
                        return;
                    }

                    created = result.Created;
                    skipped = result.SkippedShort;
                    tx.Commit();
                }

                vm.ActiveViewMarkerCount = PipeMarkerCollector.CountInView(doc, activeView);
                string skipNote = skipped > 0 ? $" ({skipped} curto(s) ignorado(s))" : string.Empty;
                vm.StatusMessage = $"Marcador bifilar criado (Ø {centerline.MeasuredDiameterMm:0.#} mm){skipNote}. Total na vista: {vm.ActiveViewMarkerCount}.";
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

        public string GetName() => "EvtBim.BifilarMarkerLinePickHandler";
    }
}
