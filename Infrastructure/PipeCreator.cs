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

            Transform transform = GetTransformForElement(element);
            List<(XYZ Start, XYZ End)> segments = ExtractSegments(geom, transform);

            if (segments.Count == 0)
                return PipeCreationResult.Failed("Geometria não suportada (apenas linhas e polylines por enquanto).");

            double offsetFeet = UnitUtils.ConvertToInternalUnits(config.OffsetMm, UnitTypeId.Millimeters);
            double targetZ = config.LevelElevationFeet + offsetFeet;
            double diameterFeet = UnitUtils.ConvertToInternalUnits(config.DiameterMm, UnitTypeId.Millimeters);
            double tol = doc.Application.ShortCurveTolerance;

            int created = 0;
            int skipped = 0;

            using (Transaction tx = new Transaction(doc, "PipeCADMapper — converter linha CAD em tubo"))
            {
                tx.Start();

                // Cria todos os segmentos como tubos e registra endpoints para conexão
                List<(Pipe Pipe, XYZ Start, XYZ End)> newPipes = new();

                foreach ((XYZ startRaw, XYZ endRaw) in segments)
                {
                    XYZ start = new XYZ(startRaw.X, startRaw.Y, targetZ);
                    XYZ end = new XYZ(endRaw.X, endRaw.Y, targetZ);

                    if (start.DistanceTo(end) < tol)
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

                    pipe.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM)?.Set(diameterFeet);

                    newPipes.Add((pipe, start, end));
                    created++;
                }

                if (created == 0)
                {
                    tx.RollBack();
                    return PipeCreationResult.Failed("Todos os segmentos eram mais curtos que a tolerância do Revit.");
                }

                // Conecta segmentos consecutivos da mesma polilinha nos vértices compartilhados.
                // O Revit insere automaticamente cotovelos/tês conforme as preferências de roteamento.
                ConnectConsecutiveSegments(newPipes, tol);

                // Conecta extremidades abertas a tubos já existentes no modelo.
                ConnectToExistingPipes(doc, newPipes, tol);

                tx.Commit();
            }

            return PipeCreationResult.Ok(created, skipped);
        }

        // Conecta o conector de saída do segmento i ao conector de entrada do segmento i+1.
        private static void ConnectConsecutiveSegments(
            List<(Pipe Pipe, XYZ Start, XYZ End)> pipes,
            double tol)
        {
            for (int i = 0; i < pipes.Count - 1; i++)
            {
                XYZ sharedPt = pipes[i].End; // == pipes[i+1].Start

                Connector? c1 = FindConnectorAt(pipes[i].Pipe, sharedPt, tol);
                Connector? c2 = FindConnectorAt(pipes[i + 1].Pipe, sharedPt, tol);

                if (c1 == null || c2 == null || c1.IsConnected || c2.IsConnected)
                    continue;

                try
                {
                    c1.ConnectTo(c2);
                }
                catch
                {
                    // Conectores incompatíveis (ex.: diâmetros discrepantes) — deixa em aberto.
                }
            }
        }

        // Busca conectores abertos em tubos existentes que coincidam com as extremidades dos novos tubos.
        private static void ConnectToExistingPipes(
            Document doc,
            List<(Pipe Pipe, XYZ Start, XYZ End)> newPipes,
            double tol)
        {
            // Índice dos IDs recém-criados para excluí-los da busca.
            HashSet<ElementId> newIds = new();
            foreach (var (pipe, _, _) in newPipes)
                newIds.Add(pipe.Id);

            // Coleta conectores abertos de todos os tubos pré-existentes.
            List<Connector> openExisting = new();
            foreach (Element el in new FilteredElementCollector(doc).OfClass(typeof(Pipe)))
            {
                if (newIds.Contains(el.Id))
                    continue;

                foreach (Connector c in ((Pipe)el).ConnectorManager.Connectors)
                {
                    if (!c.IsConnected)
                        openExisting.Add(c);
                }
            }

            if (openExisting.Count == 0)
                return;

            // Para cada extremidade aberta dos novos tubos, procura um conector vizinho.
            foreach (var (newPipe, _, _) in newPipes)
            {
                foreach (Connector newConn in newPipe.ConnectorManager.Connectors)
                {
                    if (newConn.IsConnected)
                        continue;

                    foreach (Connector existConn in openExisting)
                    {
                        if (existConn.IsConnected)
                            continue;

                        if (existConn.Origin.DistanceTo(newConn.Origin) <= tol)
                        {
                            try
                            {
                                newConn.ConnectTo(existConn);
                            }
                            catch
                            {
                                // Incompatibilidade de sistema/diâmetro — ignora.
                            }
                            break;
                        }
                    }
                }
            }
        }

        private static Connector? FindConnectorAt(Pipe pipe, XYZ point, double tol)
        {
            foreach (Connector c in pipe.ConnectorManager.Connectors)
            {
                if (c.Origin.DistanceTo(point) <= tol)
                    return c;
            }
            return null;
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
                            return gi.Transform;
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
            => new PipeCreationResult(true, created, skipped, null);

        public static PipeCreationResult Failed(string message)
            => new PipeCreationResult(false, 0, 0, message);
    }
}
