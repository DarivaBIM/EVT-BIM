using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using RevitDomain = Autodesk.Revit.DB.Domain;

namespace DarivaBIM.Revit.Adapters.Features.Prolongador
{
    /// <summary>
    /// Encontra o conector vertical de uma caixa sifonada/seca para servir de
    /// base ao prolongador. Critério: maior <c>|BasisZ.Z|</c> dentre os
    /// conectores; primeiro tenta apenas conectores do
    /// <see cref="Domain.DomainPiping"/>; se nada for encontrado, relaxa para
    /// qualquer Domain. O <see cref="ScanFor"/> grava na trilha de logs as
    /// decisões para facilitar o diagnóstico quando a caixa não tem conector
    /// vertical reconhecido.
    /// </summary>
    internal static class VerticalConnectorFinder
    {
        public static Connector? Find(FamilyInstance fi, List<string> logs)
        {
            List<Connector> connectors = new();

            try
            {
                MEPModel? mep = fi.MEPModel;
                if (mep != null)
                {
                    ConnectorManager? cm = mep.ConnectorManager;
                    if (cm != null)
                    {
                        foreach (Connector c in cm.Connectors)
                            connectors.Add(c);
                        logs.Add($"  -> Conectores via MEPModel: {connectors.Count}");
                    }
                }
            }
            catch (Exception ex)
            {
                logs.Add($"  -> ERRO ao ler conectores: {ex.Message}");
            }

            if (connectors.Count == 0)
            {
                logs.Add("  -> Nenhum conector encontrado.");
                return null;
            }

            logs.Add("  -> Buscando conector vertical (DomainPiping)...");
            List<(Connector C, double Z)> vert = ScanFor(connectors, onlyPiping: true, logs);

            if (vert.Count == 0)
            {
                logs.Add("  -> Nenhum vertical em DomainPiping. Tentando qualquer Domain...");
                vert = ScanFor(connectors, onlyPiping: false, logs);
            }

            if (vert.Count == 0)
                return null;

            vert.Sort((a, b) => b.Z.CompareTo(a.Z));
            logs.Add($"  -> Conector vertical escolhido: Origin.Z mais alto = {vert[0].Z:F3}");
            return vert[0].C;
        }

        private static List<(Connector C, double Z)> ScanFor(
            IReadOnlyList<Connector> connectors,
            bool onlyPiping,
            List<string> logs)
        {
            List<(Connector, double)> found = new();
            int idx = 0;
            foreach (Connector c in connectors)
            {
                try
                {
                    if (onlyPiping && c.Domain != RevitDomain.DomainPiping)
                    {
                        logs.Add($"  -> Conector #{idx}: Domain={c.Domain} (ignorado)");
                        idx++;
                        continue;
                    }

                    Transform cs = c.CoordinateSystem;
                    if (cs == null)
                    {
                        logs.Add($"  -> Conector #{idx}: CoordinateSystem=null (ignorado)");
                        idx++;
                        continue;
                    }

                    XYZ bz = cs.BasisZ;
                    double zabs = Math.Abs(bz.Z);

                    logs.Add(
                        $"  -> Conector #{idx}: Domain={c.Domain}, BasisZ=({bz.X:F3},{bz.Y:F3},{bz.Z:F3}), |Z|={zabs:F3}");

                    if (zabs > 0.9)
                    {
                        found.Add((c, c.Origin.Z));
                        logs.Add("     vertical");
                    }
                }
                catch (Exception ex)
                {
                    logs.Add($"  -> Conector #{idx}: erro: {ex.Message}");
                }
                idx++;
            }
            return found;
        }
    }
}
