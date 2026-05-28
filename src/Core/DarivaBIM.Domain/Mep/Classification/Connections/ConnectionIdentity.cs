using System.Collections.Generic;
using DarivaBIM.Domain.Mep.Classification.Ports;

namespace DarivaBIM.Domain.Mep.Classification.Connections
{
    /// <summary>
    /// Identidade canonica facetada de uma peca MEP classificada. Substitui o
    /// `Subtype: string` legado por enums fortes + ports[] (vide secao 5 do
    /// rulebook). Consumed pelo Tigre Catalog V2, perda de carga futura,
    /// validacoes NBR, etc.
    /// </summary>
    public sealed record ConnectionIdentity
    {
        public required Discipline Discipline { get; init; }
        public required ProductCategory Category { get; init; }
        public required BaseKind BaseKind { get; init; }

        public GeometryKind GeometryKind { get; init; } = GeometryKind.Unspecified;

        /// <summary>
        /// 45, 90, 180 ou null (Cap, MultiPort). Tolerancia do classifier:
        /// +/- 5 deg (vide `tolerances.angleDeg` no JSON rulebook).
        /// </summary>
        public double? NominalAngleDeg { get; init; }

        public required IReadOnlyList<MepPort> Ports { get; init; }

        public Feature Features { get; init; } = Feature.None;
        public ProductLine Line { get; init; } = ProductLine.Unknown;

        public required ClassificationConfidence Confidence { get; init; }

        // Granulacoes opcionais (secao 14) — populadas conforme BaseKind.
        public ValveKind? ValveKind { get; init; }
        public InstrumentKind? InstrumentKind { get; init; }
        public FilterKind? FilterKind { get; init; }
    }
}
