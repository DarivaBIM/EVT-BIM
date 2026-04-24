using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;

namespace FamiliesImporterHub.Infrastructure
{
    public static class PipeCreator
    {
        public static PipeCreationResult CreateFromReference(
            Document doc,
            Reference reference,
            PipeConversionConfig config)
        {
            Element? element = doc.GetElement(reference);
            if (element == null)
            {
                return PipeCreationResult.Failed("Elemento não encontrado.");
            }

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
            {
                return PipeCreationResult.Failed("Geometria não disponível na referência selecionada.");
            }

            Transform transform = GetTransformForElement(element);
            List<(XYZ Start, XYZ End)> segments = ExtractSegments(geom, transform);

            if (segments.Count == 0)
            {
                return PipeCreationResult.Failed("Geometria não suportada (apenas linhas e polylines por enquanto).");
            }

            double offsetFeet = UnitUtils.ConvertToInternalUnits(config.OffsetMm, UnitTypeId.Millimeters);
            double targetZ = config.LevelElevationFeet + offsetFeet;
            double diameterFeet = UnitUtils.ConvertToInternalUnits(config.DiameterMm, UnitTypeId.Millimeters);
            double shortCurveTolerance = doc.Application.ShortCurveTolerance;

            int created = 0;
            int skipped = 0;

            using (Transaction tx = new Transaction(doc, "Converter linha CAD em tubo"))
            {
                tx.Start();

                foreach ((XYZ startRaw, XYZ endRaw) in segments)
                {
                    XYZ start = new XYZ(startRaw.X, startRaw.Y, targetZ);
                    XYZ end = new XYZ(endRaw.X, endRaw.Y, targetZ);

                    if (start.DistanceTo(end) < shortCurveTolerance)
                    {
                        skipped++;
                        continue;
                    }

                    Pipe pipe = Pipe.Create(
                        doc,
                        config.SystemTypeId,
                        config.PipeTypeId,
                        config.LevelId,
                        start,
                        end);

                    Parameter? diameterParam = pipe.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM);
                    diameterParam?.Set(diameterFeet);

                    created++;
                }

                if (created == 0)
                {
                    tx.RollBack();
                    return PipeCreationResult.Failed("Todos os segmentos eram mais curtos que a tolerância do Revit.");
                }

                tx.Commit();
            }

            return PipeCreationResult.Ok(created, skipped);
        }

        private static Transform GetTransformForElement(Element element)
        {
            if (element is ImportInstance imp)
            {
                Options opts = new Options { ComputeReferences = true };
                GeometryElement? geomElem = imp.get_Geometry(opts);
                if (geomElem != null)
                {
                    foreach (GeometryObject g in geomElem)
                    {
                        if (g is GeometryInstance gi)
                        {
                            return gi.Transform;
                        }
                    }
                }
            }

            return Transform.Identity;
        }

        private static List<(XYZ Start, XYZ End)> ExtractSegments(GeometryObject geom, Transform transform)
        {
            List<(XYZ, XYZ)> segments = new();

            switch (geom)
            {
                case Line line:
                    segments.Add((
                        transform.OfPoint(line.GetEndPoint(0)),
                        transform.OfPoint(line.GetEndPoint(1))));
                    break;

                case PolyLine polyLine:
                    IList<XYZ> coords = polyLine.GetCoordinates();
                    for (int i = 0; i < coords.Count - 1; i++)
                    {
                        segments.Add((
                            transform.OfPoint(coords[i]),
                            transform.OfPoint(coords[i + 1])));
                    }
                    break;

                case Arc arc:
                    // Arcos não viram tubos curvos (Pipe.Create só aceita reta).
                    // No Passo 5 podemos decidir se vira corda reta ou é rejeitado.
                    segments.Add((
                        transform.OfPoint(arc.GetEndPoint(0)),
                        transform.OfPoint(arc.GetEndPoint(1))));
                    break;
            }

            return segments;
        }
    }

    public class PipeCreationResult
    {
        private PipeCreationResult(bool success, int createdCount, int skippedCount, string? errorMessage)
        {
            Success = success;
            CreatedCount = createdCount;
            SkippedCount = skippedCount;
            ErrorMessage = errorMessage;
        }

        public bool Success { get; }
        public int CreatedCount { get; }
        public int SkippedCount { get; }
        public string? ErrorMessage { get; }

        public static PipeCreationResult Ok(int created, int skipped)
        {
            return new PipeCreationResult(true, created, skipped, null);
        }

        public static PipeCreationResult Failed(string message)
        {
            return new PipeCreationResult(false, 0, 0, message);
        }
    }
}
