using System;
using System.Collections.Generic;

namespace DarivaBIM.Domain.Mep.Classification.Connections.Rules
{
    /// <summary>
    /// Restricoes topologicas de uma regra (secoes 12.1/19). Suporta heranca:
    /// <see cref="Inherits"/> aponta para o Id de outra regra cuja TopologyConstraint
    /// e herdada, e <see cref="Overrides"/> sobrescreve campos por cima (deep-merge
    /// resolvido pelo loader). APOS a resolucao, Inherits e Overrides ficam null e
    /// os campos carregam o valor efetivo.
    /// </summary>
    public sealed record TopologyConstraint
    {
        public IReadOnlyList<string> PartTypeAccepts { get; init; } = Array.Empty<string>();

        public int? ConnectorCount { get; init; }

        public DiameterRule? DiameterRule { get; init; }

        public AngleRange? PrimaryAngleRule { get; init; }

        public AngleRange? LateralAngleRule { get; init; }

        public string? Inherits { get; init; }

        public TopologyConstraint? Overrides { get; init; }
    }
}
