using System.Collections.Generic;
using Autodesk.Revit.DB;
using DarivaBIM.Domain.Mep.Classification.Connections;

namespace DarivaBIM.Revit.Adapters.Common.Mep
{
    /// <summary>
    /// Filtra as BOCAS FISICAS validas de um <see cref="ConnectorManager"/>
    /// (secao 9 passo 1 do rulebook). Boca fisica = porta geometrica valida da
    /// peca (End + dominio Piping + secao Round + raio real + Origin/BasisZ
    /// coerentes), INDEPENDENTEMENTE do estado de conexao na rede: uma boca LIVRE
    /// (saida aberta de um te) e tao fisica quanto uma conectada — filtrar por
    /// conexao classificaria o te como Elbow/Union e casaria codigo errado no
    /// catalogo Tigre (validado pelo Codex). Cada descarte vira um
    /// <see cref="TopologyDiagnostic"/> rastreavel. Revit-bound; a logica
    /// geometrica e do motor Domain (1.B-1) e nao mora aqui.
    ///
    /// Follow-ups previstos para o smoke da Fase 4 (NAO implementados aqui; so se
    /// aparecer conector espurio em familia mal feita): dedup geometrica de bocas
    /// coincidentes (Origin+Eixo+Raio com tolerancia) e descartar Utility==true
    /// (tap/spud auxiliares).
    /// </summary>
    internal static class ConnectorPhysicalFilter
    {
        // Raio minimo (feet) para distinguir conector fisico de conector logico
        // sem secao. Qualquer tubo real tem raio muitas ordens acima disto.
        private const double MinRadiusFeet = 1e-6;

        public static IReadOnlyList<Connector> Filter(ConnectorManager manager, List<TopologyDiagnostic> diagnostics)
        {
            var physical = new List<Connector>();

            ConnectorSet set;
            try
            {
                set = manager.Connectors;
            }
            catch
            {
                return physical;
            }

            foreach (Connector connector in set)
            {
                if (IsPhysical(connector, diagnostics))
                {
                    physical.Add(connector);
                }
            }

            return physical;
        }

        private static bool IsPhysical(Connector connector, List<TopologyDiagnostic> diagnostics)
        {
            int id = SafeId(connector);

            // 1. So conector de extremidade (End) carrega geometria de conexao real;
            // Curve/logical sao descartados.
            try
            {
                if (connector.ConnectorType != ConnectorType.End)
                {
                    Skip(diagnostics, TopologyDiagnosticCode.NonPhysicalConnectorSkipped, DiagnosticSeverity.Info,
                        $"Conector {id} nao e End.");
                    return false;
                }
            }
            catch
            {
                Skip(diagnostics, TopologyDiagnosticCode.NonPhysicalConnectorSkipped, DiagnosticSeverity.Info,
                    $"Conector {id}: falha lendo ConnectorType.");
                return false;
            }

            // 2. Dominio hidraulico (Piping). Higiene: se a leitura LANCAR, NAO
            // descarta — nao vale perder uma boca legitima por erro de leitura
            // (diferente dos demais criterios, que descartam em falha de leitura).
            try
            {
                if (connector.Domain != Autodesk.Revit.DB.Domain.DomainPiping)
                {
                    Skip(diagnostics, TopologyDiagnosticCode.DomainMismatch, DiagnosticSeverity.Info,
                        $"Conector {id} nao e Piping ({connector.Domain}).");
                    return false;
                }
            }
            catch
            {
                // Leitura de Domain falhou: deixa passar (higiene, nao descarta).
            }

            // 3. Hidraulica: so secao Round (a inferencia angular assume eixo redondo).
            try
            {
                if (connector.Shape != ConnectorProfileType.Round)
                {
                    Skip(diagnostics, TopologyDiagnosticCode.NonRoundConnectorIgnored, DiagnosticSeverity.Info,
                        $"Conector {id} nao e Round ({connector.Shape}).");
                    return false;
                }
            }
            catch
            {
                Skip(diagnostics, TopologyDiagnosticCode.NonRoundConnectorIgnored, DiagnosticSeverity.Info,
                    $"Conector {id}: falha lendo Shape.");
                return false;
            }

            // 4. Raio > 0 (Radius e seguro aqui porque ja confirmamos Round).
            try
            {
                if (connector.Radius <= MinRadiusFeet)
                {
                    Skip(diagnostics, TopologyDiagnosticCode.MissingDiameter, DiagnosticSeverity.Info,
                        $"Conector {id} sem raio.");
                    return false;
                }
            }
            catch
            {
                Skip(diagnostics, TopologyDiagnosticCode.MissingDiameter, DiagnosticSeverity.Info,
                    $"Conector {id}: falha lendo Radius.");
                return false;
            }

            // 5. Origin e BasisZ nao-degenerados. Connector.Origin lanca p/ NonEnd
            // em Revit 2025 — o try/catch cobre.
            try
            {
                XYZ origin = connector.Origin;
                XYZ basisZ = connector.CoordinateSystem.BasisZ;
                if (origin is null || basisZ is null || basisZ.IsZeroLength())
                {
                    Skip(diagnostics, TopologyDiagnosticCode.BasisZIncoherent, DiagnosticSeverity.Warning,
                        $"Conector {id} com Origin/BasisZ degenerado.");
                    return false;
                }
            }
            catch
            {
                Skip(diagnostics, TopologyDiagnosticCode.BasisZIncoherent, DiagnosticSeverity.Warning,
                    $"Conector {id}: falha lendo Origin/CoordinateSystem.");
                return false;
            }

            return true;
        }

        private static void Skip(
            List<TopologyDiagnostic> diagnostics,
            TopologyDiagnosticCode code,
            DiagnosticSeverity severity,
            string detail)
        {
            diagnostics.Add(new TopologyDiagnostic { Code = code, Severity = severity, Detail = detail });
        }

        private static int SafeId(Connector connector)
        {
            try
            {
                return connector.Id;
            }
            catch
            {
                return -1;
            }
        }
    }
}
