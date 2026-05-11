using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using DarivaBIM.Plugin.Features.PipeCadMapper.Tools;
using DarivaBIM.Presentation.Wpf.PipeConverter;
using DarivaBIM.Revit.Adapters.Common.Transactions.FailurePreprocessors;
using DarivaBIM.Revit.Adapters.Features.PipeCadMapper.Bifilar;
using DarivaBIM.Revit.Adapters.Features.PipeCadMapper.Markers;
using DarivaBIM.Revit.Adapters.Features.PipeCadMapper.Unifilar;

namespace DarivaBIM.Plugin.Features.PipeCadMapper
{
    /// <summary>
    /// Handler "one-shot" para a criação em lote de marcadores: lê o modo
    /// corrente (<see cref="PipeCadMappingMode.Unifilar"/> ou
    /// <see cref="PipeCadMappingMode.Bifilar"/>) e processa todo o layer
    /// alvo do vínculo CAD selecionado.
    /// <list type="bullet">
    /// <item>Unifilar: cada segmento reto do layer vira um marcador com o
    /// diâmetro default — o usuário fica responsável por garantir
    /// homogeneidade de diâmetros por layer.</item>
    /// <item>Bifilar: roda o <see cref="BifilarCenterlineDetector"/> com os
    /// limiares derivados do slider de Tolerância e cria um marcador por
    /// par de paredes paralelas detectado, snappando o diâmetro para a
    /// lista do tipo de tubo selecionado.</item>
    /// </list>
    /// </summary>
    public class MarkerBatchHandler : IExternalEventHandler
    {
        public PipeConverterViewModel? ViewModel { get; set; }

        public void Execute(UIApplication app)
        {
            PipeConverterViewModel? vm = ViewModel;
            if (vm == null) return;

            try
            {
                vm.IsBusy = true;

                UIDocument? uiDoc = app.ActiveUIDocument;
                if (uiDoc == null || uiDoc.Document.IsFamilyDocument)
                {
                    vm.StatusMessage = "Abra um projeto Revit para usar a ferramenta.";
                    return;
                }

                Document doc = uiDoc.Document;
                View activeView = doc.ActiveView;

                if (!vm.HasCadLink || vm.SelectedCadLinkId == null || string.IsNullOrEmpty(vm.SelectedLayer))
                {
                    vm.StatusMessage = "Selecione um vínculo CAD e um layer antes de criar marcadores em lote.";
                    return;
                }

                Element? cadElement = doc.GetElement(RevitElementIdConversions.ToElementId(vm.SelectedCadLinkId.Value));
                if (cadElement is not ImportInstance importInstance)
                {
                    vm.StatusMessage = "O vínculo CAD selecionado não está mais disponível no projeto.";
                    return;
                }

                if (!PipeMarkerConfigFactory.TryCreate(vm, out PipeMarkerConfig? config, out string? configError))
                {
                    vm.StatusMessage = configError!;
                    return;
                }

                int created;
                int skippedShort;
                string detail;

                if (vm.Mode == PipeCadMappingMode.Unifilar)
                {
                    (created, skippedShort, detail) = RunUnifilar(doc, activeView, importInstance, vm.SelectedLayer!, config!);
                }
                else
                {
                    (created, skippedShort, detail) = RunBifilar(
                        doc, activeView, importInstance, vm.SelectedLayer!, config!,
                        vm.TolerancePercent,
                        vm.SelectedPipeType?.AvailableDiametersMm ?? Array.Empty<double>());
                }

                vm.ActiveViewMarkerCount = PipeMarkerCollector.CountInView(doc, activeView);

                if (created == 0)
                {
                    vm.StatusMessage = detail.Length > 0
                        ? detail
                        : "Nenhum marcador criado. Ajuste o layer/modo/tolerância e tente novamente.";
                }
                else
                {
                    string skipNote = skippedShort > 0 ? $" ({skippedShort} curto(s) ignorado(s))" : string.Empty;
                    string extra = detail.Length > 0 ? " — " + detail : string.Empty;
                    vm.StatusMessage = $"Criado(s) {created} marcador(es){skipNote}{extra}. Total na vista: {vm.ActiveViewMarkerCount}.";
                }
            }
            catch (Exception ex)
            {
                vm.StatusMessage = "Erro ao criar marcadores em lote: " + ex.Message;
            }
            finally
            {
                vm.IsBusy = false;
            }
        }

        public string GetName() => "EvtBim.MarkerBatchHandler";

        private static (int created, int skippedShort, string detail) RunUnifilar(
            Document doc,
            View activeView,
            ImportInstance importInstance,
            string layer,
            PipeMarkerConfig config)
        {
            UnifilarSegmentBatch batch = UnifilarLineCollector.CollectFromLayer(doc, importInstance, layer);

            if (batch.Segments.Count == 0)
            {
                return (0, 0, $"Nenhuma linha reta encontrada no layer '{layer}'.");
            }

            using Transaction tx = new(doc, "PipeCADMapper — marcadores em lote (unifilar)");
            FailureHandlingOptions failureOptions = tx.GetFailureHandlingOptions();
            failureOptions.SetFailuresPreprocessor(new PipeCreationFailurePreprocessor());
            failureOptions.SetClearAfterRollback(true);
            failureOptions.SetForcedModalHandling(false);
            tx.SetFailureHandlingOptions(failureOptions);
            tx.Start();

            PipeMarkerBatch result = PipeMarkerCreator.CreateFromSegments(
                doc, activeView, batch.Segments, config);

            if (result.Created == 0)
            {
                tx.RollBack();
                return (0, 0, "Todos os segmentos eram curtos demais para criar marcadores.");
            }

            tx.Commit();

            string detail = batch.SkippedNonLinear > 0
                ? $"{batch.SkippedNonLinear} geometria(s) curva(s) ignorada(s) no layer"
                : string.Empty;

            return (result.Created, result.SkippedShort, detail);
        }

        private static (int created, int skippedShort, string detail) RunBifilar(
            Document doc,
            View activeView,
            ImportInstance importInstance,
            string layer,
            PipeMarkerConfig config,
            double tolerancePercent,
            IReadOnlyList<double> availableDiametersMm)
        {
            BifilarDetectionParameters parameters = BifilarDetectionParameters.FromTolerance(
                tolerancePercent, availableDiametersMm);

            BifilarCenterlineDetector detector = new(doc, parameters);
            IReadOnlyList<BifilarCenterline> centerlines = detector.Detect(importInstance, layer);

            if (centerlines.Count == 0)
            {
                return (0, 0, $"Nenhum par de linhas paralelas foi identificado como tubo no layer '{layer}'. Aumente a tolerância e tente novamente.");
            }

            using Transaction tx = new(doc, "PipeCADMapper — marcadores em lote (bifilar)");
            FailureHandlingOptions failureOptions = tx.GetFailureHandlingOptions();
            failureOptions.SetFailuresPreprocessor(new PipeCreationFailurePreprocessor());
            failureOptions.SetClearAfterRollback(true);
            failureOptions.SetForcedModalHandling(false);
            tx.SetFailureHandlingOptions(failureOptions);
            tx.Start();

            PipeMarkerBatch result = PipeMarkerCreator.CreateFromCenterlines(
                doc, activeView, centerlines, config, availableDiametersMm);

            if (result.Created == 0)
            {
                tx.RollBack();
                return (0, 0, "Todos os eixos detectados eram curtos demais para virar marcadores.");
            }

            tx.Commit();

            string detail = $"{centerlines.Count} eixo(s) detectado(s)";
            return (result.Created, result.SkippedShort, detail);
        }
    }
}
