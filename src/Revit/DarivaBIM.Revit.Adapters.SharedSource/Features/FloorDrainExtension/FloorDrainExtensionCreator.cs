using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;

namespace DarivaBIM.Revit.Adapters.Features.FloorDrainExtension
{
    /// <summary>
    /// Cria prolongadores (tubos verticais) acima de caixas sifonadas/secas
    /// utilizando o conector vertical da família. Orquestra:
    /// <see cref="FloorDrainExtensionSystemTypeResolver"/> (sistema),
    /// <see cref="VerticalConnectorFinder"/> (conector vertical),
    /// <see cref="FloorDrainExtensionPipeTypeResolver"/> (material → PipeType),
    /// <see cref="FloorDrainExtensionLevelResolver"/> (Level) e
    /// <see cref="FloorDrainExtensionPipeConnector"/> (conexão pipe ↔ caixa).
    /// Aceita um override opcional de <see cref="PipeType"/> por tipo de
    /// caixa (mapeado pelo <c>FamilySymbol.Id.Value</c>) — caminho que a UI
    /// usa para respeitar a escolha do usuário no dropdown.
    /// </summary>
    public static class FloorDrainExtensionCreator
    {
        public static FloorDrainExtensionResult Run(
            Document doc,
            IReadOnlyList<FamilyInstance> caixas,
            double lengthMeters,
            IReadOnlyDictionary<long, long>? pipeTypeBySymbolId = null)
        {
            FloorDrainExtensionResult result = new();

            if (caixas.Count == 0)
            {
                result.Logs.Add("Nenhuma caixa fornecida.");
                return result;
            }

            (PipingSystemType? sysType, string sysMsg) = FloorDrainExtensionSystemTypeResolver.Resolve(doc);
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

                    PipeType? pipeType = ResolvePipeTypeForBox(doc, fi, pipeTypeBySymbolId, logs);
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

                    ElementId levelId = FloorDrainExtensionLevelResolver.Resolve(doc, fi, logs);
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

                    bool connected = FloorDrainExtensionPipeConnector.ConnectPipeToFixture(novo, vert, logs);
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

        private static PipeType? ResolvePipeTypeForBox(
            Document doc,
            FamilyInstance fi,
            IReadOnlyDictionary<long, long>? pipeTypeBySymbolId,
            List<string> logs)
        {
            // 1) Override explícito da UI: a escolha do usuário no dropdown
            //    para aquele tipo de caixa tem precedência sobre a heurística
            //    de material.
            if (pipeTypeBySymbolId != null)
            {
                long symbolId = 0;
                try { symbolId = fi.Symbol.Id.Value; }
                catch { /* sem symbol legível: cai no fallback */ }

                if (symbolId != 0 &&
                    pipeTypeBySymbolId.TryGetValue(symbolId, out long pipeTypeId) &&
                    doc.GetElement(new ElementId(pipeTypeId)) is PipeType chosen)
                {
                    logs.Add($"  -> PipeType vindo da UI (override por tipo de caixa).");
                    return chosen;
                }
            }

            // 2) Fallback: heurística por material da caixa.
            string materialKind = FloorDrainExtensionPipeTypeResolver.DetermineMaterialKind(fi);
            logs.Add($"  -> Tipo material: {materialKind}");
            return FloorDrainExtensionPipeTypeResolver.FindPipeType(doc, materialKind);
        }
    }
}
