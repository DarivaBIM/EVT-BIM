using System;
using System.Collections.Generic;

namespace DarivaBIM.Domain.Mep.Classification.Lexical
{
    /// <summary>
    /// Aliases e negative tokens manuais (secao 10.2 do rulebook). Tokens
    /// armazenados na forma NORMALIZADA (sem acento, minusculo) porque o
    /// <see cref="LexicalNormalizer"/> normaliza tudo antes de comparar — ex.:
    /// "te" cobre o "tê" do rulebook ("tê" -> "te" no strip de acento), "juncao"
    /// cobre "junção". Sao equivalentes a secao 10.2 pos-normalizacao.
    /// </summary>
    public static class TokenAliases
    {
        /// <summary>
        /// Chave canonica -> variantes/sinonimos (formas normalizadas). A expansao
        /// no normalizer e BIDIRECIONAL: tanto a chave quanto qualquer variante
        /// trazem a familia inteira (ex.: "elbow" no texto adiciona "joelho", e
        /// vice-versa). Variantes que colapsam na chave pelo strip de acento ("tê"
        /// -> "te") foram omitidas por redundancia.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> Aliases =
            new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
            {
                ["te"] = new[] { "tee" },
                ["juncao"] = new[] { "wye", "lateral" },
                ["reducao"] = new[] { "redutor" },
                ["curva"] = new[] { "bend", "curve" },
                ["uniao"] = new[] { "coupling" },
                ["joelho"] = new[] { "elbow", "cotovelo" },
            };

        /// <summary>
        /// Token-alvo -> gatilhos que o suprimem quando presentes no mesmo texto
        /// (anti-falso-positivo). "te" suprimido por "terminal"/"tempo"/"teflon";
        /// "sn" por "snake"; "sr" por "sra".
        /// </summary>
        public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> NegativeTokens =
            new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
            {
                ["te"] = new[] { "terminal", "tempo", "teflon" },
                ["sn"] = new[] { "snake" },
                ["sr"] = new[] { "sra" },
            };
    }
}
