using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;

namespace DarivaBIM.Revit.Adapters.Features.Prolongador
{
    /// <summary>
    /// Cria prolongadores (tubos verticais) acima de caixas sifonadas/secas
    /// utilizando o conector vertical da família. Orquestra:
    /// <see cref="ProlongadorSystemTypeResolver"/> (sistema),
    /// <see cref="VerticalConnectorFinder"/> (conector vertical),
    /// <see cref="ProlongadorPipeTypeResolver"/> (material → PipeType),
    /// <see cref="ProlongadorLevelResolver"/> (Level) e
    /// <see cref="ProlongadorPipeConnector"/> (conexão pipe ↔ caixa).
    /// </summary>
    public static class ProlongadorCreator
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

            (PipingSystemType? sysType, string sysMsg) = ProlongadorSystemTypeResolver.Resolve(doc);
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
                logs.Add($"CAIXA #{i} - ID: {fi.Id.Value}");

                try
                {
                    Connector? vert = VerticalConnectorFinder.Find(fi, logs);
                    if (vert == null)
                    {
                        logs.Add("  -> RESULTADO: nenhum conector vertical encontrado.");
                        result.FailedNoVerticalConnector++;
                        continue;
                    }

                    string materialKind = ProlongadorPipeTypeResolver.DetermineMaterialKind(fi);
                    logs.Add($"  -> Tipo material: {materialKind}");

                    PipeType? pipeType = ProlongadorPipeTypeResolver.FindPipeType(doc, materialKind);
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

                    ElementId levelId = ProlongadorLevelResolver.Resolve(doc, fi, logs);
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

                    bool connected = ProlongadorPipeConnector.ConnectPipeToFixture(novo, vert, logs);
                    if (!connected)
                    {
                        logs.Add("  -> Aviso: pipe criado, mas não conectou automaticamente.");
                    }

                    logs.Add($"  -> SUCESSO: Pipe ID {novo.Id.Value}");
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
    }
}
