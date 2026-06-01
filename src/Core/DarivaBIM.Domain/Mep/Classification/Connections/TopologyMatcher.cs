using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DarivaBIM.Domain.Mep.Classification.Connections.Rules;
using DarivaBIM.Domain.Mep.Classification.Ports;

namespace DarivaBIM.Domain.Mep.Classification.Connections
{
    /// <summary>
    /// Filtro topologico do classificador MEP (camadas 1-2 da secao 21 do rulebook):
    /// dado um <see cref="ConnectionTopology"/> (saida do motor 1.B-1) e o rulebook ja
    /// resolvido, devolve as regras GEOMETRICAMENTE compativeis. PURO (Domain, sem
    /// Revit). NAO pontua nem escolhe vencedor (isso e a 2.B-3b) — so decide quem
    /// concorre; varios subtipos lexicais (ex.: union-simple + union-threaded) podem
    /// sobreviver ao filtro e sao desambiguados depois.
    ///
    /// Convencao de angulo (decisao 2.B-2, Codex Opcao B):
    ///   - primaryAngleRule = angulo RAW entre BasisZ outward (0..180), comparado DIRETO.
    ///   - lateralAngleRule = angulo FISICO do ramal vs eixo [0,90] = min(raw, 180-raw),
    ///     espelhando o BranchAngleVsAxis (abs(dot)) do motor.
    /// </summary>
    public static class TopologyMatcher
    {
        /// <summary>
        /// Regras do rulebook topologicamente compativeis com a topologia, na ORDEM
        /// do documento (a 2.B-3b desempata por score; a ordem JSON e o criterio
        /// estavel final). Entrada nula -> lista vazia (fail-closed, nunca lanca).
        /// </summary>
        public static IReadOnlyList<ConnectionRule> FilterCandidates(
            ConnectionRulebookDocument doc, ConnectionTopology topology)
        {
            if (doc is null || topology is null)
            {
                return Array.Empty<ConnectionRule>();
            }

            var result = new List<ConnectionRule>();
            foreach (ConnectionRule rule in doc.Rules)
            {
                if (IsCompatible(rule, topology, doc.Tolerances))
                {
                    result.Add(rule);
                }
            }

            return result;
        }

        /// <summary>
        /// True se a topologia satisfaz TODOS os criterios que a regra DEFINE (campo
        /// null = nao restringe), com a guarda anti-catch-all para regras que so tem
        /// PartTypeAccepts. AND curto-circuitado, do mais barato (count) ao mais caro.
        /// </summary>
        public static bool IsCompatible(
            ConnectionRule rule, ConnectionTopology topology, RulebookTolerances tolerances)
        {
            TopologyConstraint c = rule.Topology;

            // 1. ConnectorCount.
            if (c.ConnectorCount is int count && count != topology.Ports.Count)
            {
                return false;
            }

            // 2. PartTypeAccepts (OrdinalIgnoreCase: "Undefined" == "undefined").
            if (c.PartTypeAccepts.Count > 0
                && !c.PartTypeAccepts.Contains(topology.PartType, StringComparer.OrdinalIgnoreCase))
            {
                return false;
            }

            // 3. PrimaryAngleRule — raw DIRETO (convencao 2.B-2). Regra que exige eixo
            // numa topologia sem eixo (raw null) NAO casa (fail-closed).
            if (c.PrimaryAngleRule is not null)
            {
                double? raw = ExtractPrimaryAngleRaw(topology);
                if (raw is null || !InRange(raw.Value, c.PrimaryAngleRule))
                {
                    return false;
                }
            }

            // 4. LateralAngleRule — lateral fisico [0,90].
            if (c.LateralAngleRule is not null)
            {
                double? lateral = ExtractLateralAngle(topology);
                if (lateral is null || !InRange(lateral.Value, c.LateralAngleRule))
                {
                    return false;
                }
            }

            // 5. DiameterRule.
            if (c.DiameterRule is not null
                && !DiameterSatisfied(c.DiameterRule, topology, tolerances.DiameterMm))
            {
                return false;
            }

            // 6. Guarda anti-catch-all: regra so com PartTypeAccepts (ex.: manifold,
            // que aceita "Undefined") casaria qualquer peca. Exige o BaseKind inferido
            // pela geometria bater. decisao 2.B-3a, validar Codex panoramico.
            if (c.ConnectorCount is null
                && c.PrimaryAngleRule is null
                && c.LateralAngleRule is null
                && c.DiameterRule is null
                && topology.InferredBaseKind != rule.BaseKind)
            {
                return false;
            }

            return true;
        }

        // ----- Extracao de angulos (usam os roles que o motor 1.B-1 ja atribuiu) -----

        /// <summary>
        /// Angulo primario RAW (par do eixo) da topologia, PUBLICO p/ a cam 8 derivar a
        /// deflexao de catalogo (180 - raw) de elbows sem nominalAngleDeg fixo. Reusa a
        /// extracao interna — NAO duplicar a logica.
        /// </summary>
        public static double? PrimaryAngleRaw(ConnectionTopology topology)
            => ExtractPrimaryAngleRaw(topology);

        // Angulo primario RAW. n==2: o angulo entre as 2 bocas. n>=3: entre os runs
        // (RunA/RunB do eixo passante). Sem run claro -> null.
        private static double? ExtractPrimaryAngleRaw(ConnectionTopology topology)
        {
            int n = topology.Ports.Count;
            if (n == 2)
            {
                return AngleAt(topology, 0, 1);
            }

            if (n >= 3)
            {
                int idxA = IndexOfRole(topology, PortRole.RunA);
                int idxB = IndexOfRole(topology, PortRole.RunB);
                if (idxA >= 0 && idxB >= 0)
                {
                    return AngleAt(topology, idxA, idxB);
                }
            }

            return null;
        }

        // Angulo lateral FISICO do ramal contra um run [0,90]. min(raw, 180-raw) e
        // identico ao BranchAngleVsAxis(abs(dot)) do motor: wye com ramal a 45 real
        // gera raw 135, e min(135, 45) = 45. NUNCA o raw cru (cairia fora de {40,50}).
        private static double? ExtractLateralAngle(ConnectionTopology topology)
        {
            if (topology.Ports.Count < 3)
            {
                return null;
            }

            int idxA = IndexOfRole(topology, PortRole.RunA);
            int idxBranch = IndexOfFirstRole(
                topology, PortRole.Branch, PortRole.BranchLeft, PortRole.BranchRight);
            if (idxA < 0 || idxBranch < 0)
            {
                return null;
            }

            double? raw = AngleAt(topology, idxBranch, idxA);
            if (raw is null)
            {
                return null;
            }

            return Math.Min(raw.Value, 180.0 - raw.Value);
        }

        // Acesso defensivo a AngleMatrix[i][j]: matriz vazia / nao-quadrada / fora dos
        // limites -> null (o criterio que dependia do angulo entao falha fail-closed).
        private static double? AngleAt(ConnectionTopology topology, int i, int j)
        {
            IReadOnlyList<IReadOnlyList<double>> matrix = topology.AngleMatrix;
            if (i < 0 || j < 0 || i >= matrix.Count)
            {
                return null;
            }

            IReadOnlyList<double> row = matrix[i];
            if (row is null || j >= row.Count)
            {
                return null;
            }

            return row[j];
        }

        // Inclusivo (>= / <=): a tolerancia angular JA esta embutida no range do JSON.
        private static bool InRange(double value, AngleRange range)
            => value >= range.MinDeg && value <= range.MaxDeg;

        private static int IndexOfRole(ConnectionTopology topology, PortRole role)
        {
            for (int i = 0; i < topology.Ports.Count; i++)
            {
                if (topology.Ports[i].Role == role)
                {
                    return i;
                }
            }

            return -1;
        }

        private static int IndexOfFirstRole(ConnectionTopology topology, params PortRole[] roles)
        {
            for (int i = 0; i < topology.Ports.Count; i++)
            {
                if (Array.IndexOf(roles, topology.Ports[i].Role) >= 0)
                {
                    return i;
                }
            }

            return -1;
        }

        // ----- Diametro (port-based, secao 12.2) -----

        private static bool DiameterSatisfied(DiameterRule rule, ConnectionTopology topology, int tolMm)
        {
            // AND de todos os constraints.
            foreach (DiameterConstraint constraint in rule.Constraints)
            {
                if (!ConstraintSatisfied(constraint, topology, tolMm))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool ConstraintSatisfied(DiameterConstraint constraint, ConnectionTopology topology, int tolMm)
        {
            switch (constraint.Relation)
            {
                case DiameterRelation.Any:
                    return true;

                case DiameterRelation.Unknown:
                    // fail-closed: typo no JSON ("relation" omitido/errado) NAO vira
                    // uma constraint silenciosamente verdadeira.
                    return false;

                case DiameterRelation.Single:
                    // secao 12.2 "so 1 port"; nao exercido pelo JSON MVP (cap usa
                    // connectorCount). Semantica = peca de 1 boca.
                    return topology.Ports.Count == 1;
            }

            // Constraint com roles EXPLICITOS pressupoe que a peca os tenha. Ausencia
            // (ex.: equal[RunA,RunB] sobre um reducer com RunLarge/RunSmall) NAO e
            // vacuosamente verdadeira — senao union casaria um reducer real. Ports
            // vazio = shortcut "equal" (todos os ports), nao entra nesta guarda.
            // decisao 2.B-3a (precisao de catalogo), validar Codex panoramico.
            if (constraint.Ports.Count > 0
                && !constraint.Ports.All(role => HasRole(topology, role)))
            {
                return false;
            }

            IReadOnlyList<int> dns = ResolveDns(constraint, topology);

            switch (constraint.Relation)
            {
                case DiameterRelation.Equal:
                    // Todos os DN iguais dentro da tolerancia. 0/1 DN -> trivialmente igual.
                    return dns.Count <= 1 || (dns.Max() - dns.Min()) <= tolMm;

                case DiameterRelation.Different:
                    // Existe diferenca fora da tolerancia. <2 DN -> nao ha diferenca.
                    return dns.Count >= 2 && (dns.Max() - dns.Min()) > tolMm;

                case DiameterRelation.LessThan:
                case DiameterRelation.LessOrEqualThan:
                case DiameterRelation.GreaterThan:
                case DiameterRelation.GreaterOrEqualThan:
                    return CompareToTarget(constraint, dns, topology, tolMm);

                default:
                    return false; // fail-closed.
            }
        }

        private static IReadOnlyList<int> ResolveDns(DiameterConstraint constraint, ConnectionTopology topology)
        {
            // Ports vazio = aplica a TODOS os ports (expansao do shortcut string "equal").
            if (constraint.Ports.Count == 0)
            {
                return topology.Ports.Select(p => p.DnMm).ToList();
            }

            return topology.Ports
                .Where(p => constraint.Ports.Contains(p.Role))
                .Select(p => p.DnMm)
                .ToList();
        }

        private static bool CompareToTarget(
            DiameterConstraint constraint, IReadOnlyList<int> dns, ConnectionTopology topology, int tolMm)
        {
            double? target = ResolveTarget(constraint.Target, topology);
            if (target is null || dns.Count == 0)
            {
                // Sem target resolvivel (role ausente / string invalida) ou sem ports
                // do constraint -> fail-closed.
                return false;
            }

            // TODOS os ports do constraint devem satisfazer a relacao.
            foreach (int dn in dns)
            {
                if (!CompareDn(dn, constraint.Relation, target.Value, tolMm))
                {
                    return false;
                }
            }

            return true;
        }

        // Estritas (<, >) exigem diferenca REAL fora da tolerancia (consistente com o
        // ReductionKind do motor, que usa a mesma tolerancia de DN); nao-estritas (<=, >=)
        // toleram igualdade dentro da banda.
        private static bool CompareDn(int dn, DiameterRelation relation, double target, int tolMm)
        {
            switch (relation)
            {
                case DiameterRelation.LessThan:
                    return dn < target - tolMm;
                case DiameterRelation.LessOrEqualThan:
                    return dn <= target + tolMm;
                case DiameterRelation.GreaterThan:
                    return dn > target + tolMm;
                case DiameterRelation.GreaterOrEqualThan:
                    return dn >= target - tolMm;
                default:
                    return false;
            }
        }

        private static double? ResolveTarget(string? target, ConnectionTopology topology)
        {
            if (string.IsNullOrWhiteSpace(target))
            {
                return null;
            }

            // PortRole primeiro, MAS com IsDefined: Enum.TryParse("180") devolveria
            // (PortRole)180 com sucesso silencioso; IsDefined barra o numerico cru.
            if (Enum.TryParse(target, ignoreCase: true, out PortRole role)
                && Enum.IsDefined(typeof(PortRole), role))
            {
                int idx = IndexOfRole(topology, role);
                if (idx < 0)
                {
                    return null; // role-alvo ausente na peca -> nao avaliavel.
                }

                return topology.Ports[idx].DnMm;
            }

            if (double.TryParse(target, NumberStyles.Float, CultureInfo.InvariantCulture, out double numeric))
            {
                return numeric;
            }

            return null;
        }

        private static bool HasRole(ConnectionTopology topology, PortRole role)
        {
            for (int i = 0; i < topology.Ports.Count; i++)
            {
                if (topology.Ports[i].Role == role)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
