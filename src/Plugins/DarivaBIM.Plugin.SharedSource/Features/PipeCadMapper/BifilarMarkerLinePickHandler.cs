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

                // Extrai a cadeia de vértices da geometria picada. Line → 2
                // vértices; PolyLine → N vértices. Valida o layer via
                // UnifilarLineCollector pra reusar a checagem já existente.
                if (UnifilarLineCollector.CollectFromReference(doc, geom, transform, vm.SelectedLayer!) == null)
                {
                    vm.StatusMessage = $"A linha clicada não está no layer '{vm.SelectedLayer}'.";
                    return;
                }

                List<XYZ> anchorVertices = new();
                switch (geom)
                {
                    case Line line:
                        anchorVertices.Add(transform.OfPoint(line.GetEndPoint(0)));
                        anchorVertices.Add(transform.OfPoint(line.GetEndPoint(1)));
                        break;
                    case PolyLine pl:
                        IList<XYZ> rawPts = pl.GetCoordinates();
                        for (int i = 0; i < rawPts.Count; i++)
                            anchorVertices.Add(transform.OfPoint(rawPts[i]));
                        break;
                }

                if (anchorVertices.Count < 2)
                {
                    vm.StatusMessage = "Geometria não suportada (apenas linhas e polylines retas).";
                    return;
                }

                IReadOnlyList<double> availableDiameters =
                    vm.SelectedPipeType?.AvailableDiametersMm ?? Array.Empty<double>();

                // Picker usa parâmetros independentes do slider — bem mais
                // permissivos que o batch (overlap mínimo, ângulo, símbolos).
                // O ÚNICO filtro estrito mantido é ±2mm de algum nominal,
                // que mora dentro do detector e vale para qualquer caminho.
                BifilarDetectionParameters parameters = BifilarDetectionParameters.ForPicker(availableDiameters);

                BifilarCenterlineDetector detector = new(doc, parameters);
                IReadOnlyList<BifilarCenterline> centerlines = detector.DetectForAnchor(
                    importInstance, vm.SelectedLayer!, anchorVertices);

                if (centerlines.Count == 0)
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
                        doc, activeView, centerlines, config!, availableDiameters);

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
                if (centerlines.Count == 1)
                {
                    double diamMm = centerlines[0].MeasuredDiameterMm;
                    vm.StatusMessage = $"Marcador bifilar criado (Ø {diamMm:0.#} mm){skipNote}. Total na vista: {vm.ActiveViewMarkerCount}.";
                }
                else
                {
                    // Polyline pareada: vários marcadores conectados seguindo
                    // os bends. Mostra a contagem em vez de um único diâmetro.
                    vm.StatusMessage = $"Traçado bifilar criado: {created} marcador(es){skipNote}. Total na vista: {vm.ActiveViewMarkerCount}.";
                }
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
