using System;
using System.Collections.Generic;

namespace DarivaBIM.Domain.Mep.Classification.Connections.Rules
{
    /// <summary>
    /// Uma regra do rulebook (secao 19): a identidade canonica (BaseKind/GeometryKind/
    /// angulo) + restricoes topologicas + disambiguators lexicais. POCO desserializado
    /// do JSON; apos o loader, a <see cref="Topology"/> ja vem resolvida (sem inherits
    /// pendente). BaseKind/GeometryKind sao os enums do namespace Connections (ancestral).
    /// </summary>
    public sealed record ConnectionRule
    {
        public string Id { get; init; } = "";

        public BaseKind BaseKind { get; init; }

        public GeometryKind? GeometryKind { get; init; }

        public double? NominalAngleDeg { get; init; }

        public TopologyConstraint Topology { get; init; } = new();

        public IReadOnlyList<LexicalDisambiguator> LexicalDisambiguators { get; init; }
            = Array.Empty<LexicalDisambiguator>();

        public IReadOnlyList<string> LexicalHints { get; init; } = Array.Empty<string>();

        public bool RequiresLexicalConfirmation { get; init; }
    }
}
