using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;

namespace DarivaBIM.Revit.Adapters.V2025.Features.Prolongador
{
    /// <summary>
    /// Conecta o tubo prolongador ao conector vertical da caixa: escolhe o
    /// <see cref="Connector"/> do tubo mais próximo do <c>Origin</c> do
    /// fixture connector e tenta <c>ConnectTo</c>; se falhar, tenta o
    /// inverso para acomodar regras assimétricas do Revit.
    /// </summary>
    internal static class ProlongadorPipeConnector
    {
        public static bool ConnectPipeToFixture(Pipe pipe, Connector fixtureConnector, List<string> logs)
        {
            ConnectorManager? cm = pipe.ConnectorManager;
            if (cm == null)
            {
                logs.Add("  -> Aviso: pipe sem ConnectorManager.");
                return false;
            }

            Connector? closest = null;
            double bestDist = double.MaxValue;

            foreach (Connector c in cm.Connectors)
            {
                try
                {
                    double d = c.Origin.DistanceTo(fixtureConnector.Origin);
                    if (d < bestDist)
                    {
                        bestDist = d;
                        closest = c;
                    }
                }
                catch
                {
                    // ignora
                }
            }

            if (closest == null)
            {
                logs.Add("  -> Aviso: não encontrei conector do pipe para conectar.");
                return false;
            }

            try
            {
                closest.ConnectTo(fixtureConnector);
                logs.Add($"  -> Conectado (dist {bestDist:F6} ft).");
                return true;
            }
            catch
            {
                try
                {
                    fixtureConnector.ConnectTo(closest);
                    logs.Add("  -> Conectado (fallback invertido).");
                    return true;
                }
                catch (System.Exception ex2)
                {
                    logs.Add($"  -> Falha ao conectar: {ex2.Message}");
                    return false;
                }
            }
        }
    }
}
