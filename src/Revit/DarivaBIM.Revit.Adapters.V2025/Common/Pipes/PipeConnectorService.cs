using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;

namespace DarivaBIM.Revit.Adapters.V2025.Common.Pipes
{
    /// <summary>
    /// Connector-level helpers used by the pipe-creation flow: pairing
    /// vertices between consecutive placeholders before the placeholder→pipe
    /// conversion, and stitching the freshly created pipes to any pre-existing
    /// open connectors that happen to coincide in space.
    /// </summary>
    public static class PipeConnectorService
    {
        public static void ConnectConsecutivePlaceholders(
            List<(Pipe Pipe, XYZ Start, XYZ End)> placeholders,
            double tol)
        {
            for (int i = 0; i < placeholders.Count - 1; i++)
            {
                XYZ sharedPoint = placeholders[i].End;
                TryConnectPipesAt(placeholders[i].Pipe, placeholders[i + 1].Pipe, sharedPoint, tol);
            }

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

        public static void TryConnectPipesAt(Pipe a, Pipe b, XYZ sharedPoint, double tol)
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

        public static void ConnectToExistingPipes(
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

        public static Connector? FindConnectorAt(Pipe pipe, XYZ point, double tol)
        {
            foreach (Connector c in pipe.ConnectorManager.Connectors)
            {
                if (c.Origin.DistanceTo(point) <= tol)
                    return c;
            }
            return null;
        }
    }
}
