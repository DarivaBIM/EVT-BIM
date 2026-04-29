using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using DarivaBIM.Revit.Adapters.V2026.Common.Cad;
using DarivaBIM.Revit.Adapters.V2026.Common.Pipes;
using DarivaBIM.Revit.Adapters.V2026.Common.Transactions.FailurePreprocessors;

namespace DarivaBIM.Revit.Adapters.V2026.Features.PipeCadMapper
{
    /// <summary>
    /// Fachada da feature PipeCADMapper: a partir de uma <see cref="Reference"/>
    /// para uma curva de vínculo CAD, cria os tubos correspondentes no Revit.
    /// Geometria, segmentação, criação de placeholders, conversão em tubos e
    /// gerenciamento de conectores ficam em classes dedicadas; aqui só mora
    /// a orquestração: transação, preprocessor de falhas e a sequência das
    /// etapas.
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

            using Transaction tx = new(doc, "PipeCADMapper — converter linha CAD em tubo");
            FailureHandlingOptions failureOptions = tx.GetFailureHandlingOptions();
            failureOptions.SetFailuresPreprocessor(new PipeCreationFailurePreprocessor());
            failureOptions.SetClearAfterRollback(true);
            failureOptions.SetForcedModalHandling(false);
            tx.SetFailureHandlingOptions(failureOptions);
            tx.Start();

            // 1) Cria placeholders na cota de destino e armazena para conexão posterior.
            PipePlaceholderBatch batch = PipePlaceholderCreator.CreatePlaceholders(
                doc, segments, config, targetZ, diameterFeet, tol);

            if (batch.Created == 0)
            {
                tx.RollBack();
                return PipeCreationResult.Failed("Todos os segmentos eram mais curtos que a tolerância do Revit.");
            }

            // 2) Conecta extremidades coincidentes ANTES da conversão para que o
            //    PlumbingUtils insira automaticamente joelhos/tês nos vértices.
            PipeConnectorService.ConnectConsecutivePlaceholders(batch.Placeholders, tol);

            // 3) Converte placeholders em tubos reais. O Revit injeta as peças
            //    de conexão automaticamente conforme as preferências de roteamento.
            List<Pipe> convertedPipes;
            try
            {
                convertedPipes = PipePlaceholderConverter.Convert(doc, batch.Placeholders);
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
            PipeConnectorService.ConnectToExistingPipes(doc, convertedPipes, tol);

            tx.Commit();

            return PipeCreationResult.Ok(batch.Created, batch.Skipped, arcChordCount);
        }
    }
}
