using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using DarivaBIM.Revit.Adapters.V2026.Cad;
using DarivaBIM.Revit.Adapters.V2026.Mapping;
using DarivaBIM.Revit.Adapters.V2026.Pipes;
using DarivaBIM.Revit.Adapters.V2026.Transactions;

namespace DarivaBIM.Revit.Adapters.V2026.Writers
{
    /// <summary>
    /// Façade temporária que orquestra a criação de tubos a partir de uma
    /// referência geométrica de vínculo CAD. Geometria, segmentação e
    /// gerenciamento de conectores foram extraídos para
    /// <see cref="CadGeometryExtractor"/>, <see cref="CadSegmentExtractor"/> e
    /// <see cref="PipeConnectorService"/>; a transação e a inserção de
    /// placeholders permanecem aqui até o próximo passo da quebra
    /// (ver ADR-0010).
    /// </summary>
    public static class PipeCreator
    {
        public static PipeCreationResult CreateFromReference(
            Document doc,
            Reference reference,
            PipeConversionConfig config)
        {
            Element? element = doc.GetElement(reference);
            if (element == null)
                return PipeCreationResult.Failed("Elemento não encontrado.");

            GeometryObject? geom;
            try
            {
                geom = element.GetGeometryObjectFromReference(reference);
            }
            catch
            {
                geom = null;
            }

            if (geom == null)
                return PipeCreationResult.Failed("Geometria não disponível na referência selecionada.");

            Transform transform = CadGeometryExtractor.GetTransformForElement(element);
            List<(XYZ Start, XYZ End)> segments = CadSegmentExtractor.ExtractSegments(geom, transform, out int arcChordCount);

            if (segments.Count == 0)
                return PipeCreationResult.Failed("Geometria não suportada (apenas linhas e polylines por enquanto).");

            double offsetFeet = UnitUtils.ConvertToInternalUnits(config.OffsetMm, UnitTypeId.Millimeters);
            double targetZ = config.LevelElevationFeet + offsetFeet;
            double diameterFeet = UnitUtils.ConvertToInternalUnits(config.DiameterMm, UnitTypeId.Millimeters);
            double tol = doc.Application.ShortCurveTolerance;

            int created = 0;
            int skipped = 0;

            using Transaction tx = new(doc, "PipeCADMapper — converter linha CAD em tubo");
            FailureHandlingOptions failureOptions = tx.GetFailureHandlingOptions();
            failureOptions.SetFailuresPreprocessor(new PipeCreationFailurePreprocessor());
            failureOptions.SetClearAfterRollback(true);
            failureOptions.SetForcedModalHandling(false);
            tx.SetFailureHandlingOptions(failureOptions);
            tx.Start();

            // 1) Cria placeholders na cota de destino e armazena para conexão posterior.
            List<(Pipe Pipe, XYZ Start, XYZ End)> placeholders = new(segments.Count);

            foreach ((XYZ startRaw, XYZ endRaw) in segments)
            {
                XYZ start = new(startRaw.X, startRaw.Y, targetZ);
                XYZ end = new(endRaw.X, endRaw.Y, targetZ);

                if (start.DistanceTo(end) < tol)
                {
                    skipped++;
                    continue;
                }

                Pipe placeholder = Pipe.CreatePlaceholder(
                    doc,
                    config.SystemTypeId,
                    config.PipeTypeId,
                    config.LevelId,
                    start,
                    end);

                placeholder.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM)?.Set(diameterFeet);

                placeholders.Add((placeholder, start, end));
                created++;
            }

            if (created == 0)
            {
                tx.RollBack();
                return PipeCreationResult.Failed("Todos os segmentos eram mais curtos que a tolerância do Revit.");
            }

            // 2) Conecta extremidades coincidentes ANTES da conversão para que o
            //    PlumbingUtils insira automaticamente joelhos/tês nos vértices.
            PipeConnectorService.ConnectConsecutivePlaceholders(placeholders, tol);

            // 3) Converte placeholders em tubos reais. O Revit injeta as peças
            //    de conexão automaticamente conforme as preferências de roteamento.
            List<ElementId> placeholderIds = new(placeholders.Count);
            foreach (var (pipe, _, _) in placeholders)
                placeholderIds.Add(pipe.Id);

            ICollection<ElementId> convertedIds;
            try
            {
                convertedIds = PlumbingUtils.ConvertPipePlaceholders(doc, placeholderIds);
            }
            catch (Exception ex)
            {
                tx.RollBack();
                return PipeCreationResult.Failed(
                    "Falha ao converter placeholders em tubos: " + ex.Message +
                    " — verifique as preferências de roteamento (joelhos/tês) do tipo de tubo.");
            }

            // 4) Após a conversão, tenta plugar extremidades abertas em tubos
            //    pré-existentes do modelo.
            List<Pipe> convertedPipes = new(convertedIds.Count);
            foreach (ElementId id in convertedIds)
            {
                if (doc.GetElement(id) is Pipe p)
                    convertedPipes.Add(p);
            }

            PipeConnectorService.ConnectToExistingPipes(doc, convertedPipes, tol);

            tx.Commit();

            return PipeCreationResult.Ok(created, skipped, arcChordCount);
        }
    }

    public sealed class PipeCreationResult
    {
        private PipeCreationResult(
            bool success,
            int createdCount,
            int skippedCount,
            int arcsAsChordCount,
            string? errorMessage)
        {
            Success = success;
            CreatedCount = createdCount;
            SkippedCount = skippedCount;
            ArcsAsChordCount = arcsAsChordCount;
            ErrorMessage = errorMessage;
        }

        public bool Success { get; }
        public int CreatedCount { get; }
        public int SkippedCount { get; }
        public int ArcsAsChordCount { get; }
        public string? ErrorMessage { get; }

        public static PipeCreationResult Ok(int created, int skipped, int arcsAsChord = 0)
            => new(true, created, skipped, arcsAsChord, null);

        public static PipeCreationResult Failed(string message)
            => new(false, 0, 0, 0, message);
    }
}
