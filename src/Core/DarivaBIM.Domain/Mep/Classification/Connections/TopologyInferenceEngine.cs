using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using DarivaBIM.Domain.Mep.Classification.Ports;

namespace DarivaBIM.Domain.Mep.Classification.Connections
{
    /// <summary>
    /// Motor de inferencia topologica PURO (Domain, sem Revit) — o coracao
    /// testavel da classificacao MEP. Recebe os conectores fisicos ja extraidos
    /// (<see cref="ConnectorReading"/>) e infere BaseKind, PortRoles, matrizes de
    /// angulo/distancia, ReductionKind e diagnosticos por contagem + geometria.
    /// Algoritmo da secao 9 do rulebook, COM a correcao C1/D1 do roadmap: a
    /// matriz de angulos e RAW (Acos(clamp(dot,-1,1)) em 0..180, sem abs) para que
    /// peca reta (anti-paralela, ~180) nao colapse com peca a 0 grau; o abs entra
    /// SO na medicao do ramal contra o eixo (que e uma reta nao-orientada).
    /// A AngleMatrix carrega o angulo RAW entre OutwardNormals (0..180); para
    /// JOELHOS a deflexao (o angulo do catalogo "Joelho 45/90") = 180 - raw, e o
    /// motor NAO converte (joelho 90 -> raw 90; joelho 45 -> raw 135) — a derivacao
    /// e da fase 2.B (errata rulebook secao 9, 2026-05-28).
    /// NominalAngleDeg/GeometryKind NAO sao saida deste motor: ConnectionTopology
    /// nao os carrega (vivem em ConnectionIdentity, derivados na fase 2.B a partir
    /// da matriz raw aqui produzida).
    /// </summary>
    public static class TopologyInferenceEngine
    {
        /// <summary>
        /// Infere a topologia de uma peca a partir dos conectores fisicos lidos.
        /// Retorna sempre um <see cref="TopologyReadResult"/> (nunca lanca):
        /// leitura sem conector vira Success=false + diagnostico Error.
        /// </summary>
        public static TopologyReadResult Infer(
            IReadOnlyList<ConnectorReading> readings,
            string partTypeRaw,
            Discipline discipline,
            ProductCategory category,
            TopologyInferenceOptions? opts = null)
        {
            opts ??= new TopologyInferenceOptions();
            var diagnostics = new List<TopologyDiagnostic>();

            // count == 0: sem conector fisico apos filtro -> leitura invalida (Error),
            // Topology null. O caller degrada o pipeline em vez de crashar.
            if (readings is null || readings.Count == 0)
            {
                diagnostics.Add(new TopologyDiagnostic
                {
                    Code = TopologyDiagnosticCode.InsufficientConnectorsAfterFilter,
                    Severity = DiagnosticSeverity.Error,
                    Detail = "Nenhum conector fisico apos filtro.",
                });
                return new TopologyReadResult
                {
                    Success = false,
                    Topology = null,
                    Diagnostics = diagnostics,
                };
            }

            // Ordena por NativeIndex: matriz e roles passam a ser deterministas
            // (a ordem de iteracao do Revit nao vaza para o resultado).
            var ordered = readings.OrderBy(r => r.NativeIndex).ToList();
            int count = ordered.Count;

            var angleMatrix = BuildAngleMatrix(ordered);
            var distanceMatrix = BuildDistanceMatrix(ordered);

            // Par anti-paralelo (eixo passante) = angulo >= InlineMinDeg E maior
            // distancia entre Origins. Itera (i<j); em empate de distancia mantem o
            // primeiro encontrado (menor i, depois menor j) => determinista.
            bool isInline = false;
            int inlineI = -1;
            int inlineJ = -1;
            double inlineDist = double.NegativeInfinity;
            for (int i = 0; i < count; i++)
            {
                for (int j = i + 1; j < count; j++)
                {
                    if (angleMatrix[i][j] >= opts.InlineMinDeg && distanceMatrix[i][j] > inlineDist)
                    {
                        isInline = true;
                        inlineI = i;
                        inlineJ = j;
                        inlineDist = distanceMatrix[i][j];
                    }
                }
            }

            var roles = new PortRole[count];
            BaseKind inferred;

            if (count == 1)
            {
                // Unica extremidade aberta -> Cap.
                inferred = BaseKind.Cap;
                roles[0] = PortRole.Outlet;
            }
            else if (count == 2)
            {
                bool equalDn = Math.Abs(ordered[0].DnMm - ordered[1].DnMm) <= opts.DnEqualToleranceMm;
                AssignTwoRoles(ordered, equalDn, roles);
                if (isInline)
                {
                    // Reta: DN igual = Union (luva), DN diferente = Reducer.
                    inferred = equalDn ? BaseKind.Union : BaseKind.Reducer;
                }
                else
                {
                    // Angulada: joelho (deflexao lida na matriz raw).
                    inferred = BaseKind.Elbow;
                }
            }
            else if (count == 3 && isInline)
            {
                int branch = RemainingIndex(count, inlineI, inlineJ);
                roles[inlineI] = PortRole.RunA;
                roles[inlineJ] = PortRole.RunB;
                roles[branch] = PortRole.Branch;
                double branchAngle = BranchAngleVsAxis(ordered[branch].OutwardNormal, ordered[inlineI].OutwardNormal);
                if (branchAngle >= opts.LateralTeeMinDeg && branchAngle <= opts.LateralTeeMaxDeg)
                {
                    inferred = BaseKind.Tee;
                }
                else if (branchAngle >= opts.LateralWyeMinDeg && branchAngle <= opts.LateralWyeMaxDeg)
                {
                    inferred = BaseKind.Wye;
                }
                else
                {
                    // Ramal fora das faixas canonicas (~90 Tee / ~45 Wye): ainda e
                    // peca de 3 vias com ramal; classifica Tee por default e deixa o
                    // refinamento (lexical/angular) para a fase 2.B.
                    inferred = BaseKind.Tee;
                }
            }
            else if (count == 4 && isInline)
            {
                roles[inlineI] = PortRole.RunA;
                roles[inlineJ] = PortRole.RunB;
                AssignCrossBranches(count, inlineI, inlineJ, roles);
                // Cruzeta (laterais ~90) vs juncao dupla (laterais ~45) e distincao
                // de subtipo refinada na 2.B; ambas sao BaseKind=Cross.
                inferred = BaseKind.Cross;
            }
            else if (count >= 5)
            {
                // Manifold/distribuidor.
                inferred = BaseKind.MultiPort;
                AssignMultiPortRoles(roles);
            }
            else
            {
                // count 3 ou 4 SEM par inline: anomalia geometrica (fitting que
                // deveria ter eixo passante e nao tem). Classifica MultiPort e
                // sinaliza; o PartType nativo (se houver) pode dar pista na 2.B.
                inferred = BaseKind.MultiPort;
                AssignMultiPortRoles(roles);
                diagnostics.Add(new TopologyDiagnostic
                {
                    Code = TopologyDiagnosticCode.OriginOutsideExpectedIntersection,
                    Severity = DiagnosticSeverity.Warning,
                    Detail = $"{count} conectores sem par anti-paralelo (eixo passante) detectado.",
                });
            }

            var reduction = InferReduction(ordered, count);

            var ports = new MepPort[count];
            for (int t = 0; t < count; t++)
            {
                ports[t] = MakePort(ordered[t], roles[t]);
            }

            // Cross-check do PartType nativo (HINT FRACO, D7): a geometria prevalece.
            var hint = PartTypeHints.ToBaseKindHint(partTypeRaw);
            if (hint is null)
            {
                diagnostics.Add(new TopologyDiagnostic
                {
                    Code = TopologyDiagnosticCode.PartTypeUndefined,
                    Severity = DiagnosticSeverity.Info,
                    Detail = $"PartType '{partTypeRaw}' sem hint; classificado por geometria como {inferred}.",
                });
            }
            else if (hint.Value != inferred)
            {
                diagnostics.Add(new TopologyDiagnostic
                {
                    Code = TopologyDiagnosticCode.PartTypeMismatchInferred,
                    Severity = DiagnosticSeverity.Warning,
                    Detail = $"PartType '{partTypeRaw}' sugere {hint.Value} mas a geometria infere {inferred}; geometria prevalece.",
                });
            }

            var topology = new ConnectionTopology
            {
                PartType = partTypeRaw ?? "",
                Ports = ports,
                AngleMatrix = angleMatrix,
                DistanceMatrix = distanceMatrix,
                InferredBaseKind = inferred,
                InferredDiscipline = discipline,
                InferredCategory = category,
                ReductionKind = reduction,
                IsInlinePairDetected = isInline,
            };

            return new TopologyReadResult
            {
                Success = true,
                Topology = topology,
                Diagnostics = diagnostics,
            };
        }

        private static IReadOnlyList<IReadOnlyList<double>> BuildAngleMatrix(IReadOnlyList<ConnectorReading> c)
        {
            int n = c.Count;
            var matrix = new List<IReadOnlyList<double>>(n);
            for (int i = 0; i < n; i++)
            {
                var row = new double[n];
                for (int j = 0; j < n; j++)
                {
                    row[j] = AngleDeg(c[i].OutwardNormal, c[j].OutwardNormal);
                }

                matrix.Add(row);
            }

            return matrix;
        }

        private static IReadOnlyList<IReadOnlyList<double>> BuildDistanceMatrix(IReadOnlyList<ConnectorReading> c)
        {
            int n = c.Count;
            var matrix = new List<IReadOnlyList<double>>(n);
            for (int i = 0; i < n; i++)
            {
                var row = new double[n];
                for (int j = 0; j < n; j++)
                {
                    row[j] = (c[i].Origin - c[j].Origin).Length();
                }

                matrix.Add(row);
            }

            return matrix;
        }

        // Angulo RAW (0..180) entre dois OutwardNormals. Sem abs: peca reta tem
        // normais anti-paralelos (~180), peca a 0 grau tem normais paralelos (~0).
        private static double AngleDeg(Vector3 a, Vector3 b)
        {
            var na = SafeNormalize(a);
            var nb = SafeNormalize(b);
            double dot = Clamp(Vector3.Dot(na, nb), -1.0, 1.0);
            return Math.Acos(dot) * 180.0 / Math.PI;
        }

        // Angulo do ramal contra o eixo passante. Abs no dot porque o eixo e uma
        // reta NAO-orientada: um ramal a 45 e a 135 graus do vetor do eixo e o
        // mesmo angulo lateral fisico (vide C1 do roadmap).
        private static double BranchAngleVsAxis(Vector3 branchNormal, Vector3 axisNormal)
        {
            var nb = SafeNormalize(branchNormal);
            var na = SafeNormalize(axisNormal);
            double dot = Clamp(Math.Abs(Vector3.Dot(nb, na)), 0.0, 1.0);
            return Math.Acos(dot) * 180.0 / Math.PI;
        }

        private static Vector3 SafeNormalize(Vector3 v)
        {
            float len = v.Length();
            // BasisZ degenerado e responsabilidade do filtro fisico (Adapter 1.B-2);
            // aqui apenas evitamos divisao por zero deixando o vetor como esta.
            if (len < 1e-6f)
            {
                return v;
            }

            return v / len;
        }

        // netstandard2.0 nao tem Math.Clamp.
        private static double Clamp(double v, double lo, double hi)
        {
            if (v < lo)
            {
                return lo;
            }

            if (v > hi)
            {
                return hi;
            }

            return v;
        }

        private static void AssignTwoRoles(IReadOnlyList<ConnectorReading> ordered, bool equalDn, PortRole[] roles)
        {
            if (equalDn)
            {
                roles[0] = PortRole.RunA;
                roles[1] = PortRole.RunB;
            }
            else
            {
                // DN diferente -> RunLarge/RunSmall por diametro.
                int large = ordered[0].DnMm >= ordered[1].DnMm ? 0 : 1;
                roles[large] = PortRole.RunLarge;
                roles[large == 0 ? 1 : 0] = PortRole.RunSmall;
            }
        }

        private static void AssignCrossBranches(int count, int inlineI, int inlineJ, PortRole[] roles)
        {
            // Os dois conectores fora do par passante viram BranchLeft/BranchRight;
            // menor NativeIndex (lista ja ordenada) = Left => determinista.
            bool leftAssigned = false;
            for (int t = 0; t < count; t++)
            {
                if (t == inlineI || t == inlineJ)
                {
                    continue;
                }

                if (!leftAssigned)
                {
                    roles[t] = PortRole.BranchLeft;
                    leftAssigned = true;
                }
                else
                {
                    roles[t] = PortRole.BranchRight;
                }
            }
        }

        private static void AssignMultiPortRoles(PortRole[] roles)
        {
            // §6: manifold = Inlet + multiplos Branch. Sem direcao de fluxo,
            // convenciona o de menor NativeIndex (roles[0]) como Inlet e o resto
            // como Branch. Determinista por construcao.
            roles[0] = PortRole.Inlet;
            for (int t = 1; t < roles.Length; t++)
            {
                roles[t] = PortRole.Branch;
            }
        }

        private static int RemainingIndex(int count, int i, int j)
        {
            for (int t = 0; t < count; t++)
            {
                if (t != i && t != j)
                {
                    return t;
                }
            }

            return -1; // inalcancavel em count==3 com i != j
        }

        private static ReductionKind InferReduction(IReadOnlyList<ConnectorReading> ordered, int count)
        {
            int distinctDns = ordered.Select(r => r.DnMm).Distinct().Count();
            if (distinctDns <= 1)
            {
                return ReductionKind.None;
            }

            if (count == 2)
            {
                // Eccentric exigiria detectar offset do eixo (Origins fora da reta
                // dos normais); nao detectado nesta slice -> Concentric por default.
                return ReductionKind.Concentric;
            }

            // 3+ conectores com DN variado: a reducao num fitting com ramal e
            // tipicamente no ramal (tee-reducer / wye-reducer).
            return ReductionKind.BranchOnly;
        }

        private static MepPort MakePort(ConnectorReading r, PortRole role) => new()
        {
            Role = role,
            DnMm = r.DnMm,
            Direction = r.OutwardNormal,
            Origin = r.Origin,
            Shape = r.Shape,
        };
    }
}
