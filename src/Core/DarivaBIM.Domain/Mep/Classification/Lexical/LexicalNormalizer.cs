using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace DarivaBIM.Domain.Mep.Classification.Lexical
{
    /// <summary>
    /// Tokenizador lexical do modulo Mep (secao 10.1 do rulebook). Codigo PROPRIO
    /// e INDEPENDENTE de Tigre (decisao D5): espelha a tecnica unicode de
    /// TigreTextUtils (FormD + descarte de NonSpacingMark + lower) como codigo novo,
    /// SEM referenciar nem depender de DarivaBIM.Domain.Tigre. Duplicacao consciente
    /// e documentada — o matcher v1 (pago/validado) nao e tocado e Mep.Classification
    /// nao conhece Tigre.
    ///
    /// Pipeline: strip de acento (preserva case) -> separa dimensoes coladas (x/X/×
    /// SO entre digitos) -> camelCase split (sem estilhacar siglas) -> lower -> split
    /// por separadores -> expande aliases (familia de sinonimos) -> remove negative
    /// tokens. O resultado e um CONJUNTO ordenado (primeira aparicao), entao "te"
    /// casa como token isolado e nunca como substring de "terminal".
    /// </summary>
    public static class LexicalNormalizer
    {
        // Separadores fixos. x/X NAO entram aqui (quebrariam "Redux"); sao tratados
        // so entre digitos pela DimensionCrossRegex. × (U+00D7) sempre separa.
        private static readonly Regex SeparatorRegex =
            new(@"[\s_\-./×]+", RegexOptions.Compiled);

        // x/X/× entre digitos = separador dimensional ("25x50" -> "25 50").
        private static readonly Regex DimensionCrossRegex =
            new(@"(?<=\d)\s*[xX×]\s*(?=\d)", RegexOptions.Compiled);

        // camelCase: minuscula seguida de maiuscula ("fooBar").
        private static readonly Regex CamelLowerUpperRegex =
            new(@"(?<=[a-z])(?=[A-Z])", RegexOptions.Compiled);

        // Fim de sigla seguido de palavra capitalizada ("ESGRedux" -> "ESG Redux").
        // Siglas puras (PPR, PN, SR, SN) nao casam por nao terem [a-z] na sequencia.
        private static readonly Regex CamelAcronymWordRegex =
            new(@"(?<=[A-Z])(?=[A-Z][a-z])", RegexOptions.Compiled);

        // Indice de familias de alias do conjunto padrao, construido uma vez.
        private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> DefaultAliasFamilies =
            BuildAliasFamilies(TokenAliases.Aliases);

        public static IReadOnlyList<string> Tokenize(string? raw, TokenizerOptions opts)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return Array.Empty<string>();
            }

            string text = raw!;

            // 1. Strip de acento ANTES do lower preservando o case (o camelCase
            // split depende das maiusculas).
            if (opts.StripAccents)
            {
                text = StripAccents(text);
            }

            // 2. Dimensoes coladas: "25x50" -> "25 50" (x/X/× so entre digitos).
            if (opts.SplitOnSeparators)
            {
                text = DimensionCrossRegex.Replace(text, " ");
            }

            // 3. camelCase split (usa o case, antes do lower).
            if (opts.SplitCamelCase)
            {
                text = CamelLowerUpperRegex.Replace(text, " ");
                text = CamelAcronymWordRegex.Replace(text, " ");
            }

            // 4. Lower.
            if (opts.ToLowerInvariant)
            {
                text = text.ToLowerInvariant();
            }

            // 5. Split por separadores fixos -> conjunto ordenado (1a aparicao).
            string[] pieces = opts.SplitOnSeparators
                ? SeparatorRegex.Split(text)
                : new[] { text };

            var tokens = new List<string>(pieces.Length);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string piece in pieces)
            {
                if (piece.Length > 0 && seen.Add(piece))
                {
                    tokens.Add(piece);
                }
            }

            // 6. Expande a familia de sinonimos de cada token (bidirecional).
            if (opts.ExpandAliases)
            {
                IReadOnlyDictionary<string, IReadOnlyList<string>> families =
                    opts.Aliases is null ? DefaultAliasFamilies : BuildAliasFamilies(opts.Aliases);
                ExpandAliases(tokens, seen, families);
            }

            // 7. Remove negative tokens (gatilho presente suprime o alvo).
            return RemoveNegatives(tokens, opts.NegativeTokens ?? TokenAliases.NegativeTokens);
        }

        // FormD decompoe o acento; descartamos os NonSpacingMark e recompomos em
        // FormC. Equivalente a tecnica de TigreTextUtils, reimplementada aqui (D5).
        private static string StripAccents(string text)
        {
            string decomposed = text.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(decomposed.Length);
            foreach (char ch in decomposed)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                {
                    sb.Append(ch);
                }
            }

            return sb.ToString().Normalize(NormalizationForm.FormC);
        }

        private static string NormalizeToken(string term)
            => StripAccents(term).ToLowerInvariant().Trim();

        // Constroi, por termo (chave OU variante, normalizado), a familia inteira de
        // sinonimos. Assume familias disjuntas entre entradas (os dados garantem).
        private static IReadOnlyDictionary<string, IReadOnlyList<string>> BuildAliasFamilies(
            IReadOnlyDictionary<string, IReadOnlyList<string>> aliases)
        {
            var families = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, IReadOnlyList<string>> kv in aliases)
            {
                var family = new List<string>();
                AddNormalized(family, kv.Key);
                foreach (string variant in kv.Value)
                {
                    AddNormalized(family, variant);
                }

                foreach (string member in family)
                {
                    families[member] = family;
                }
            }

            return families;
        }

        private static void AddNormalized(List<string> family, string term)
        {
            string norm = NormalizeToken(term);
            if (norm.Length > 0 && !family.Contains(norm))
            {
                family.Add(norm);
            }
        }

        private static void ExpandAliases(
            List<string> tokens,
            HashSet<string> seen,
            IReadOnlyDictionary<string, IReadOnlyList<string>> families)
        {
            // Itera sobre uma copia: o conjunto de origem nao muda durante a expansao.
            int originalCount = tokens.Count;
            for (int i = 0; i < originalCount; i++)
            {
                if (families.TryGetValue(tokens[i], out IReadOnlyList<string>? family))
                {
                    foreach (string member in family)
                    {
                        if (seen.Add(member))
                        {
                            tokens.Add(member);
                        }
                    }
                }
            }
        }

        private static IReadOnlyList<string> RemoveNegatives(
            List<string> tokens,
            IReadOnlyDictionary<string, IReadOnlyList<string>> negatives)
        {
            if (tokens.Count == 0 || negatives.Count == 0)
            {
                return tokens;
            }

            var present = new HashSet<string>(tokens, StringComparer.Ordinal);
            var toRemove = new HashSet<string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, IReadOnlyList<string>> kv in negatives)
            {
                foreach (string trigger in kv.Value)
                {
                    if (present.Contains(NormalizeToken(trigger)))
                    {
                        toRemove.Add(NormalizeToken(kv.Key));
                        break;
                    }
                }
            }

            if (toRemove.Count == 0)
            {
                return tokens;
            }

            var result = new List<string>(tokens.Count);
            foreach (string token in tokens)
            {
                if (!toRemove.Contains(token))
                {
                    result.Add(token);
                }
            }

            return result;
        }
    }
}
