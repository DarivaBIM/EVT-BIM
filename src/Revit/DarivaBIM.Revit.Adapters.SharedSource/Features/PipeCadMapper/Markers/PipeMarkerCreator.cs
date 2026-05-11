using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using DarivaBIM.Revit.Adapters.Features.PipeCadMapper.Bifilar;

namespace DarivaBIM.Revit.Adapters.Features.PipeCadMapper.Markers
{
    /// <summary>
    /// Cria marcadores (placeholders de tubo taggeados como
    /// DBIM_PIPE_MARKER, com override visual magenta) a partir de
    /// segmentos retos (unifilar) ou eixos centrais detectados (bifilar).
    /// A transação é aberta pelo caller — esta classe só cria os elementos
    /// dentro dela.
    /// </summary>
    public static class PipeMarkerCreator
    {
        /// <summary>
        /// Cria marcadores em modo unifilar: cada segmento vira um marcador
        /// com o diâmetro default da config.
        /// </summary>
        public static PipeMarkerBatch CreateFromSegments(
            Document doc,
            View activeView,
            IReadOnlyList<(XYZ Start, XYZ End)> segments,
            PipeMarkerConfig config)
        {
            int created = 0;
            int skippedShort = 0;
            double tol = doc.Application.ShortCurveTolerance;
            double defaultDiameterFt = UnitUtils.ConvertToInternalUnits(config.DefaultDiameterMm, UnitTypeId.Millimeters);
            double levelOffsetFt = UnitUtils.ConvertToInternalUnits(config.OffsetMm, UnitTypeId.Millimeters);
            double targetZWhenLevel = config.LevelElevationFeet + levelOffsetFt;

            foreach ((XYZ startRaw, XYZ endRaw) in segments)
            {
                double zStart = config.UseCadElevation ? startRaw.Z : targetZWhenLevel;
                double zEnd = config.UseCadElevation ? endRaw.Z : targetZWhenLevel;

                XYZ start = new(startRaw.X, startRaw.Y, zStart);
                XYZ end = new(endRaw.X, endRaw.Y, zEnd);

                if (start.DistanceTo(end) < tol)
                {
                    skippedShort++;
                    continue;
                }

                Pipe placeholder = Pipe.CreatePlaceholder(
                    doc,
                    config.SystemTypeId,
                    config.PipeTypeId,
                    config.LevelId,
                    start,
                    end);

                placeholder.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM)?.Set(defaultDiameterFt);

                PipeMarkerTag.Apply(placeholder);
                PipeMarkerOverrides.Apply(activeView, placeholder.Id);

                created++;
            }

            return new PipeMarkerBatch(created, skippedShort);
        }

        /// <summary>
        /// Cria marcadores em modo bifilar: cada eixo central detectado vira
        /// um marcador com o diâmetro arredondado para a lista de diâmetros
        /// disponíveis no tipo selecionado.
        /// </summary>
        public static PipeMarkerBatch CreateFromCenterlines(
            Document doc,
            View activeView,
            IReadOnlyList<BifilarCenterline> centerlines,
            PipeMarkerConfig config,
            IReadOnlyList<double> availableDiametersMm)
        {
            int created = 0;
            int skippedShort = 0;
            double tol = doc.Application.ShortCurveTolerance;
            double levelOffsetFt = UnitUtils.ConvertToInternalUnits(config.OffsetMm, UnitTypeId.Millimeters);
            double targetZWhenLevel = config.LevelElevationFeet + levelOffsetFt;

            foreach (BifilarCenterline center in centerlines)
            {
                double zStart = config.UseCadElevation ? center.Start.Z : targetZWhenLevel;
                double zEnd = config.UseCadElevation ? center.End.Z : targetZWhenLevel;

                XYZ start = new(center.Start.X, center.Start.Y, zStart);
                XYZ end = new(center.End.X, center.End.Y, zEnd);

                if (start.DistanceTo(end) < tol)
                {
                    skippedShort++;
                    continue;
                }

                double diameterMm = DiameterSnapper.Snap(
                    center.MeasuredDiameterMm,
                    availableDiametersMm,
                    config.DefaultDiameterMm);
                double diameterFt = UnitUtils.ConvertToInternalUnits(diameterMm, UnitTypeId.Millimeters);

                Pipe placeholder = Pipe.CreatePlaceholder(
                    doc,
                    config.SystemTypeId,
                    config.PipeTypeId,
                    config.LevelId,
                    start,
                    end);

                placeholder.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM)?.Set(diameterFt);

                PipeMarkerTag.Apply(placeholder);
                PipeMarkerOverrides.Apply(activeView, placeholder.Id);

                created++;
            }

            return new PipeMarkerBatch(created, skippedShort);
        }
    }

    public readonly struct PipeMarkerBatch
    {
        public PipeMarkerBatch(int created, int skippedShort)
        {
            Created = created;
            SkippedShort = skippedShort;
        }

        public int Created { get; }
        public int SkippedShort { get; }
    }
}
