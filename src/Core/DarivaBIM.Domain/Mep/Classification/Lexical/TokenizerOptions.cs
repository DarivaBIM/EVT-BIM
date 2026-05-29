using System.Collections.Generic;

namespace DarivaBIM.Domain.Mep.Classification.Lexical
{
    /// <summary>
    /// Opcoes do <see cref="LexicalNormalizer"/> (secao 10.1 do rulebook). Record
    /// init-only para permitir variar o comportamento em teste/calibracao sem
    /// mutar o normalizer. Aliases/NegativeTokens default null -> o normalizer cai
    /// nos padroes de <see cref="TokenAliases"/> (mantem este record sem dependencia
    /// dos dados; o fallback mora no normalizer).
    /// </summary>
    public sealed record TokenizerOptions
    {
        /// <summary>Remove acentos via FormD + descarte de NonSpacingMark (Soldavel).</summary>
        public bool StripAccents { get; init; } = true;

        public bool ToLowerInvariant { get; init; } = true;

        /// <summary>Separadores: espaco _ - . / × (e x/X SO entre digitos: "25x50").</summary>
        public bool SplitOnSeparators { get; init; } = true;

        /// <summary>camelCase split ("ESGRedux" -> esg redux) sem estilhacar siglas.</summary>
        public bool SplitCamelCase { get; init; } = true;

        /// <summary>
        /// Match so como token isolado, nunca substring ("te" nao casa em
        /// "terminal"). No design atual o output e um conjunto de tokens, entao o
        /// boundary e SEMPRE garantido pela tokenizacao; a flag fica explicita para
        /// o contrato da secao 10.1 e reservada a um eventual modo de substring.
        /// </summary>
        public bool RequireBoundary { get; init; } = true;

        /// <summary>Expande a familia de sinonimos de cada token (vide LexicalNormalizer).</summary>
        public bool ExpandAliases { get; init; } = true;

        /// <summary>Aliases custom; null usa <see cref="TokenAliases.Aliases"/>.</summary>
        public IReadOnlyDictionary<string, IReadOnlyList<string>>? Aliases { get; init; }

        /// <summary>Negative tokens custom; null usa <see cref="TokenAliases.NegativeTokens"/>.</summary>
        public IReadOnlyDictionary<string, IReadOnlyList<string>>? NegativeTokens { get; init; }
    }
}
