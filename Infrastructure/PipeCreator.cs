using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;

namespace FamiliesImporterHub.Infrastructure
{
    /// <summary>
    /// Converte uma referência geométrica de um vínculo CAD (linha ou polilinha)
    /// em tubos do Revit, garantindo que conexões (joelhos/tês) sejam criadas
    /// automaticamente entre segmentos adjacentes.
    /// </summary>
    /// <remarks>
    /// Estratégia: cria primeiro <c>PipePlaceholder</c>s para cada segmento,
    /// conecta os conectores compartilhados nos vértices da polilinha e então
    /// chama <c>PlumbingUtils.ConvertPipePlaceholders</c>. A conversão usa as
    /// preferências de roteamento do <c>PipeType</c> para inserir as peças de
    /// conexão (joelho 45/90, tê, redução etc.) de forma automática.
    /// </remarks>
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
            List<(XYZ Start, XYZ End)> segments = ExtractSegments(geom, transform, out int arcChordCount);

            if (segments.Count == 0)
                return PipeCreationResult.Failed("Geometria não suportada (apenas linhas e polylines por enquanto).");

            double offsetFeet = UnitUtils.ConvertToInternalUnits(config.OffsetMm, UnitTypeId.Millimeters);
            double targetZ = config.LevelElevationFeet + offsetFeet;
            double diameterFeet = UnitUtils.ConvertToInternalUnits(config.DiameterMm, UnitTypeId.Millimeters);
            double tol = doc.Application.ShortCurveTolerance;

            int created = 0;
            int skipped = 0;

            using Transaction tx = new(doc, "PipeCADMapper — converter linha CAD em tubo");
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
            ConnectConsecutivePlaceholders(placeholders, tol);

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

            ConnectToExistingPipes(doc, convertedPipes, tol);

            tx.Commit();

            return PipeCreationResult.Ok(created, skipped, arcChordCount);
        }

        /// <summary>
        /// Liga conectores coincidentes entre placeholders adjacentes da mesma
        /// polilinha. Quando dois placeholders compartilham o vértice, esta
        /// ligação informa ao <c>ConvertPipePlaceholders</c> que ali deve haver
        /// uma conexão (joelho/tê).
        /// </summary>
        private static void ConnectConsecutivePlaceholders(
            List<(Pipe Pipe, XYZ Start, XYZ End)> placeholders,
            double tol)
        {
            // Conecta vértices consecutivos da polilinha.
            for (int i = 0; i < placeholders.Count - 1; i++)
            {
                XYZ sharedPoint = placeholders[i].End; // == placeholders[i + 1].Start
                TryConnectPipesAt(placeholders[i].Pipe, placeholders[i + 1].Pipe, sharedPoint, tol);
            }

            // Cobre o caso de polilinhas fechadas (último vértice = primeiro).
            if (placeholders.Count > 2)
            {
                var first = placeholders[0];
                var last = placeholders[^1];
                if (last.End.DistanceTo(first.Start) <= tol)
                {
                    TryConnectPipesAt(last.Pipe, first.Pipe, first.Start, tol);
                }
            }
        }

        private static void TryConnectPipesAt(Pipe a, Pipe b, XYZ sharedPoint, double tol)
        {
            Connector? ca = FindConnectorAt(a, sharedPoint, tol);
            Connector? cb = FindConnectorAt(b, sharedPoint, tol);

            if (ca == null || cb == null || ca.IsConnected || cb.IsConnected)
                return;

            try
            {
                ca.ConnectTo(cb);
            }
            catch
            {
                // Conectores incompatíveis (ex.: diâmetros distintos) —
                // o ConvertPipePlaceholders ainda assim tentará resolver.
            }
        }

        /// <summary>
        /// Após a conversão, conecta extremidades abertas dos novos tubos a
        /// conectores também abertos de tubos pré-existentes que coincidam em
        /// posição (dentro da tolerância).
        /// </summary>
        private static void ConnectToExistingPipes(
            Document doc,
            IReadOnlyList<Pipe> newPipes,
            double tol)
        {
            if (newPipes.Count == 0)
                return;

            HashSet<ElementId> newIds = new();
            foreach (Pipe p in newPipes)
                newIds.Add(p.Id);

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

            foreach (Pipe newPipe in newPipes)
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
                                // Sistema/diâmetro incompatível — ignora.
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
                Options opts = new() { ComputeReferences = true };
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

        private static List<(XYZ Start, XYZ End)> ExtractSegments(
            GeometryObject geom,
            Transform transform,
            out int arcChordCount)
        {
            arcChordCount = 0;
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
                    // Pipe.CreatePlaceholder só aceita linhas retas; arcos viram corda.
                    arcChordCount = 1;
                    segments.Add((
                        transform.OfPoint(arc.GetEndPoint(0)),
                        transform.OfPoint(arc.GetEndPoint(1))));
                    break;
            }

            return segments;
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
