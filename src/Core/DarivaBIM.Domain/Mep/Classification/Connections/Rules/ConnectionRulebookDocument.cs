using System;
using System.Collections.Generic;

namespace DarivaBIM.Domain.Mep.Classification.Connections.Rules
{
    /// <summary>
    /// Raiz do JSON do rulebook (secao 19): metadados + dicionarios lexicais +
    /// tolerancias + as regras. Desserializado pelo <see cref="ConnectionRulebookLoader"/>,
    /// que resolve os inherits e valida o documento. Discipline fica como string
    /// (metadado do documento); a resolucao por disciplina e da fase 2.B.
    /// </summary>
    public sealed record ConnectionRulebookDocument
    {
        public string Version { get; init; } = "";

        public string Discipline { get; init; } = "";

        public IReadOnlyDictionary<string, IReadOnlyList<string>> BaseKindTokens { get; init; }
            = new Dictionary<string, IReadOnlyList<string>>();

        public IReadOnlyDictionary<string, IReadOnlyList<string>> TokenAliases { get; init; }
            = new Dictionary<string, IReadOnlyList<string>>();

        public IReadOnlyDictionary<string, IReadOnlyList<string>> NegativeTokens { get; init; }
            = new Dictionary<string, IReadOnlyList<string>>();

        public RulebookTolerances Tolerances { get; init; } = new();

        public IReadOnlyList<ConnectionRule> Rules { get; init; } = Array.Empty<ConnectionRule>();
    }
}
