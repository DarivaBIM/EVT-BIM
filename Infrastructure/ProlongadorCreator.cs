using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;

namespace FamiliesImporterHub.Infrastructure
{
    /// <summary>
    /// Cria prolongadores (tubos verticais) acima de caixas sifonadas/secas
    /// utilizando o conector vertical da família. Reproduz a lógica do script
    /// Dynamo: detecta material (redux/reforçada/série normal) pelos campos da
    /// instância e escolhe o <c>PipeType</c> correspondente; usa o primeiro
    /// <c>PipingSystemType</c> de esgoto/sanitário disponível no projeto.
    /// </summary>
    public sealed class ProlongadorResult
    {
        public int Created { get; set; }
        public int FailedNoVerticalConnector { get; set; }
        public int FailedNoPipeType { get; set; }
        public int FailedOther { get; set; }
        public List<string> Logs { get; } = new();
    }

    internal static class ProlongadorCreator
    {
        public static ProlongadorResult Run(
            Document doc,
            IReadOnlyList<FamilyInstance> caixas,
            double lengthMeters)
        {
            ProlongadorResult result = new();

            if (caixas.Count == 0)
            {
                result.Logs.Add("Nenhuma caixa fornecida.");
                return result;
            }

            (PipingSystemType? sysType, string sysMsg) = FindPipingSystemType(doc);
            result.Logs.Add(sysMsg);

            if (sysType == null)
            {
                result.Logs.Add("ERRO: não existe PipingSystemType no projeto.");
                return result;
            }

            double lengthFeet = UnitUtils.ConvertToInternalUnits(lengthMeters, UnitTypeId.Meters);

            using Transaction tx = new(doc, "Tigre — Adicionar prolongadores em caixas");
            tx.Start();

            for (int i = 0; i < caixas.Count; i++)
            {
                FamilyInstance fi = caixas[i];
                List<string> logs = new();
                logs.Add(new string('=', 60));
                logs.Add($"CAIXA #{i} - ID: {fi.Id.IntegerValue}");

                try
                {
                    Connector? vert = PickVerticalConnector(fi, logs);
                    if (vert == null)
                    {
                        logs.Add("  -> RESULTADO: nenhum conector vertical encontrado.");
                        result.FailedNoVerticalConnector++;
                        continue;
                    }

                    string materialKind = DetermineMaterialKind(fi);
                    logs.Add($"  -> Tipo material: {materialKind}");

                    PipeType? pipeType = FindPipeType(doc, materialKind);
                    if (pipeType == null)
                    {
                        logs.Add("  -> ERRO: nenhum PipeType encontrado.");
                        result.FailedNoPipeType++;
                        continue;
                    }

                    logs.Add($"  -> PipeType: '{pipeType.Name}'");

                    double diameterFeet;
                    try
                    {
                        diameterFeet = vert.Radius * 2.0;
                        logs.Add($"  -> Diâmetro do conector: {diameterFeet:F6} ft");
                    }
                    catch (Exception ex)
                    {
                        logs.Add($"  -> ERRO lendo raio do conector: {ex.Message}");
                        result.FailedOther++;
                        continue;
                    }

                    XYZ p0 = vert.Origin;
                    XYZ p1 = new(p0.X, p0.Y, p0.Z + lengthFeet);

                    ElementId levelId = GetLevelIdForElement(doc, fi, logs);
                    if (levelId == ElementId.InvalidElementId)
                    {
                        logs.Add("  -> ERRO: não consegui determinar Level válido.");
                        result.FailedOther++;
                        continue;
                    }

                    Pipe novo = Pipe.Create(doc, sysType.Id, pipeType.Id, levelId, p0, p1);

                    Parameter? pDiam = novo.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM);
                    if (pDiam != null && !pDiam.IsReadOnly)
                    {
                        try
                        {
                            pDiam.Set(diameterFeet);
                            logs.Add("  -> Diâmetro ajustado no tubo.");
                        }
                        catch (Exception ex)
                        {
                            logs.Add($"  -> Aviso: não consegui ajustar diâmetro: {ex.Message}");
                        }
                    }

                    bool connected = ConnectPipeToFixture(novo, vert, logs);
                    if (!connected)
                    {
                        logs.Add("  -> Aviso: pipe criado, mas não conectou automaticamente.");
                    }

                    logs.Add($"  -> SUCESSO: Pipe ID {novo.Id.IntegerValue}");
                    result.Created++;
                }
                catch (Exception ex)
                {
                    logs.Add($"  -> ERRO inesperado: {ex.Message}");
                    result.FailedOther++;
                }
                finally
                {
                    result.Logs.AddRange(logs);
                }
            }

            tx.Commit();
            return result;
        }

        private static string DetermineMaterialKind(FamilyInstance fi)
        {
            List<string> fields = new();

            try { fields.Add(fi.Symbol.Family.Name ?? string.Empty); } catch { }
            try { fields.Add(fi.Symbol.Name ?? string.Empty); } catch { }
            try { fields.Add(fi.Name ?? string.Empty); } catch { }

            try
            {
                Parameter? p = fi.get_Parameter(BuiltInParameter.ALL_MODEL_DESCRIPTION);
                if (p != null && p.HasValue)
                    fields.Add(p.AsString() ?? string.Empty);
            }
            catch { }

            try
            {
                Parameter? p = fi.Symbol.get_Parameter(BuiltInParameter.ALL_MODEL_TYPE_COMMENTS);
                if (p != null && p.HasValue)
                    fields.Add(p.AsString() ?? string.Empty);
            }
            catch { }

            foreach (string f in fields)
                if (Contains(f, "redux"))
                    return "redux";

            foreach (string f in fields)
                if (Contains(f, "reforcada") || Contains(f, "reforçada"))
                    return "reforcada";

            return "serie normal";
        }

        private static PipeType? FindPipeType(Document doc, string materialKind)
        {
            List<PipeType> types = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_PipeCurves)
                .WhereElementIsElementType()
                .OfType<PipeType>()
                .ToList();

            if (types.Count == 0)
                return null;

            if (materialKind == "redux")
            {
                // Prioridade: "Redux" + "Prolongamento/Prolongador".
                foreach (PipeType pt in types)
                {
                    string n = pt.Name ?? string.Empty;
                    if (Contains(n, "redux") &&
                        (Contains(n, "prolongamento") || Contains(n, "prolongador")))
                        return pt;
                }

                foreach (PipeType pt in types)
                {
                    string n = pt.Name ?? string.Empty;
                    if (Contains(n, "redux"))
                        return pt;
                }
            }

            foreach (PipeType pt in types)
            {
                string n = pt.Name ?? string.Empty;

                if (materialKind == "reforcada" &&
                    (Contains(n, "reforcada") || Contains(n, "reforçada")))
                    return pt;

                if (materialKind == "serie normal" && Contains(n, "serie normal"))
                    return pt;
            }

            return types[0];
        }

        private static (PipingSystemType?, string) FindPipingSystemType(Document doc)
        {
            string[] preferences =
            {
                "esgoto", "sanitario", "sanitário", "waste", "sanitary",
                "vent", "dreno", "drain",
            };

            List<PipingSystemType> types = new FilteredElementCollector(doc)
                .OfClass(typeof(PipingSystemType))
                .Cast<PipingSystemType>()
                .ToList();

            if (types.Count == 0)
                return (null, "Nenhum PipingSystemType encontrado no projeto.");

            foreach (string pref in preferences)
            {
                foreach (PipingSystemType t in types)
                {
                    string n = t.Name ?? string.Empty;
                    if (Contains(n, pref))
                        return (t, $"PipingSystemType escolhido: '{n}' (match: {pref})");
                }
            }

            return (types[0], $"PipingSystemType fallback: '{types[0].Name}'");
        }

        private static Connector? PickVerticalConnector(FamilyInstance fi, List<string> logs)
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

            List<(Connector C, double Z)> ScanFor(bool onlyPiping)
            {
                List<(Connector, double)> found = new();
                int idx = 0;
                foreach (Connector c in connectors)
                {
                    try
                    {
                        if (onlyPiping && c.Domain != Domain.DomainPiping)
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

            logs.Add("  -> Buscando conector vertical (DomainPiping)...");
            List<(Connector C, double Z)> vert = ScanFor(true);

            if (vert.Count == 0)
            {
                logs.Add("  -> Nenhum vertical em DomainPiping. Tentando qualquer Domain...");
                vert = ScanFor(false);
            }

            if (vert.Count == 0)
                return null;

            vert.Sort((a, b) => b.Z.CompareTo(a.Z));
            logs.Add($"  -> Conector vertical escolhido: Origin.Z mais alto = {vert[0].Z:F3}");
            return vert[0].C;
        }

        private static bool ConnectPipeToFixture(Pipe pipe, Connector fixtureConnector, List<string> logs)
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
                catch (Exception ex2)
                {
                    logs.Add($"  -> Falha ao conectar: {ex2.Message}");
                    return false;
                }
            }
        }

        private static ElementId GetLevelIdForElement(Document doc, Element elem, List<string> logs)
        {
            try
            {
                if (elem.LevelId != null && elem.LevelId != ElementId.InvalidElementId)
                    return elem.LevelId;
            }
            catch { }

            try
            {
                View? v = doc.ActiveView;
                if (v is ViewPlan vp && vp.GenLevel != null)
                {
                    logs.Add("  -> LevelId inválido, usando GenLevel da vista ativa.");
                    return vp.GenLevel.Id;
                }
            }
            catch { }

            Element? lvl = new FilteredElementCollector(doc).OfClass(typeof(Level)).FirstElement();
            if (lvl != null)
            {
                logs.Add("  -> LevelId inválido, usando primeiro Level do projeto.");
                return lvl.Id;
            }

            return ElementId.InvalidElementId;
        }

        private static bool Contains(string text, string needle)
        {
            string a = TigreTextUtils.Normalize(text);
            string b = TigreTextUtils.Normalize(needle);
            return !string.IsNullOrEmpty(b) && a.IndexOf(b, StringComparison.Ordinal) >= 0;
        }
    }
}
