using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace DarivaBIM.Revit.Adapters.Features.UtilizationPoints
{
    /// <summary>
    /// Helpers de baixo nível em torno de conectores hidráulicos: leitura via
    /// <see cref="ConnectorManager"/>, filtragem por "livre" (ponta + não
    /// conectado), direção e conector mais próximo de um ponto. Equivalente em
    /// C# das funções homônimas do script Python de referência.
    /// </summary>
    internal static class RevitConnectorUtilities
    {
        public static ConnectorManager? GetConnectorManager(Element element)
        {
            if (element == null) return null;

            try
            {
                if (element is MEPCurve curve && curve.ConnectorManager != null)
                    return curve.ConnectorManager;
            }
            catch { /* segue */ }

            try
            {
                if (element is FamilyInstance fi && fi.MEPModel != null)
                    return fi.MEPModel.ConnectorManager;
            }
            catch { /* segue */ }

            return null;
        }

        public static IReadOnlyList<Connector> GetConnectors(Element element)
        {
            ConnectorManager? cm = GetConnectorManager(element);
            if (cm == null) return Array.Empty<Connector>();

            List<Connector> result = new();
            try
            {
                foreach (Connector c in cm.Connectors)
                    result.Add(c);
            }
            catch { /* devolve o que conseguiu coletar */ }

            return result;
        }

        public static bool IsFreeEndConnector(Connector connector)
        {
            if (connector == null) return false;

            try
            {
                if (connector.ConnectorType != ConnectorType.End) return false;
            }
            catch { return false; }

            try
            {
                if (connector.IsConnected) return false;
            }
            catch { return false; }

            return true;
        }

        public static IReadOnlyList<Connector> GetFreeConnectors(Element element)
        {
            IReadOnlyList<Connector> all = GetConnectors(element);
            if (all.Count == 0) return Array.Empty<Connector>();

            List<Connector> free = new();
            for (int i = 0; i < all.Count; i++)
            {
                if (IsFreeEndConnector(all[i])) free.Add(all[i]);
            }
            return free;
        }

        public static XYZ? GetDirection(Connector connector)
        {
            if (connector == null) return null;
            try
            {
                Transform cs = connector.CoordinateSystem;
                if (cs == null) return null;
                return cs.BasisZ.Normalize();
            }
            catch
            {
                return null;
            }
        }

        public static Connector? GetClosestConnector(Element element, XYZ point, bool onlyFree)
        {
            IReadOnlyList<Connector> connectors = onlyFree
                ? GetFreeConnectors(element)
                : GetConnectors(element);

            if (connectors.Count == 0) return null;

            Connector? closest = null;
            double bestDistance = double.MaxValue;

            for (int i = 0; i < connectors.Count; i++)
            {
                Connector c = connectors[i];
                try
                {
                    double d = c.Origin.DistanceTo(point);
                    if (d < bestDistance)
                    {
                        bestDistance = d;
                        closest = c;
                    }
                }
                catch
                {
                    // Ignora conectores cuja origem não pode ser lida.
                }
            }

            return closest;
        }
    }
}
