using System;
using System.Collections.Generic;

namespace DarivaBIM.Domain.Mep.Classification.Connections.Rules
{
    /// <summary>
    /// Promove o match para um subtipo-filho quando o gatilho lexical aparece no
    /// texto (secao 11). So promove se a topologia do filho for compativel
    /// (<see cref="TopologyMustMatch"/>) e todos os <see cref="MandatoryLexical"/>
    /// estiverem presentes. <see cref="PromoteTo"/> referencia o Id de outra regra.
    /// </summary>
    public sealed record LexicalDisambiguator
    {
        public string Trigger { get; init; } = "";

        public string PromoteTo { get; init; } = "";

        public IReadOnlyList<string> MandatoryLexical { get; init; } = Array.Empty<string>();

        public bool TopologyMustMatch { get; init; }
    }
}
