using System.Collections.Generic;

namespace DarivaBIM.Domain.Mep.Classification.Connections
{
    /// <summary>
    /// Veredito de confianca anexado a cada ConnectionIdentity para a UI poder
    /// destacar resultados duvidosos e para auditoria. Score, bucket e reasons
    /// sao acoplados (vide secao 16 do rulebook canonico) — manter juntos
    /// evita drift entre o numero exibido e o motivo apresentado.
    /// </summary>
    public sealed record ClassificationConfidence
    {
        /// <summary>
        /// Score 0..1 calculado pelo classifier conforme secao 16.2 do rulebook.
        /// Bucket UI deriva via thresholds High &gt;= 0.75, Medium &gt;= 0.45, else Low.
        /// </summary>
        public required double Score { get; init; }

        public required ConfidenceBucket Bucket { get; init; }

        /// <summary>
        /// Reason codes acumulados durante classificacao (vide secao 16.3 — ex:
        /// `TopologyMatched:Elbow90`, `LexicalHint:joelho@familyName`, ...).
        /// Lista vazia indica identity Unknown / fallback.
        /// </summary>
        public required IReadOnlyList<string> Reasons { get; init; }
    }
}
