using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace DarivaBIM.Domain.Tigre
{
    public static class TigreTextUtils
    {
        private static readonly char[] Separators =
        {
            ';', ',', '.', '/', '\\', '-', '_', '(', ')', '[', ']', '{', '}', ':',
        };

        private static readonly char[] WordSplit = new[] { ' ' };

        // Strip ordenado de marcadores dimensionais que poluem o token
        // matching: a descrição "lean" preserva apenas o que identifica o
        // PRODUTO (família, tipo, variação), nunca a especificação
        // (diâmetro, pressão, comprimento). SR/SN/REDUX/Soldável/Roscável,
        // Curta/Longa e acentos NÃO são strippados — diferenciam produtos.
        private static readonly Regex StripDnRegex = new(
            @"\b(?:DN|dn)\s*\d+(?:[xX]\d+)?(?:[xX]\d+)?(?:\s*mm)?\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex StripMmRegex = new(
            @"\b\d+(?:[xX]\d+)?(?:[xX]\d+)?\s*mm\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // 9 variantes de aspas/apóstrofes que aparecem em descrições de
        // polegada — reta, apóstrofe, agudo, curvas (copy/paste de Word)
        // e os primes Unicode usados pela ABNT.
        //   U+0022 " quote reto          U+0027 ' apóstrofe reto
        //   U+00B4 ´ agudo               U+2018 ‘ single curve esq
        //   U+2019 ’ single curve dir    U+201C “ double curve esq
        //   U+201D ” double curve dir    U+2032 ′ prime
        //   U+2033 ″ double prime
        private static readonly string InchQuoteChars =
            "\u0022\u0027\u00B4\u2018\u2019\u201C\u201D\u2032\u2033";

        // Pré-strip: "25x3/4\"", "75x2.1/2\"" — mm × polegada fracionária
        // com aspas. Aplicado ANTES do StripInchRegex pra eliminar o par
        // como unidade; senão o main strip consome só metade e deixa
        // "25x3/" como lixo no lean.
        private static readonly Regex StripFractionInchRegex = new(
            @"\b\d+(?:\.\d+)?[xX]\d+(?:\.\d+)?/\d+\s*[" + InchQuoteChars + "]",
            RegexOptions.Compiled);

        // Pré-strip: "2.1/2'x2'", "1.1/4\"x3/4\"" — polegada × polegada
        // com aspas no meio. Sem isso o main strip consome só metade
        // e deixa "x2'" sobrando.
        private static readonly Regex StripFractionInchPairRegex = new(
            @"\b\d+(?:\.\d+)?(?:/\d+)?\s*[" + InchQuoteChars + @"]\s*[xX]\s*" +
            @"\d+(?:\.\d+)?(?:/\d+)?\s*[" + InchQuoteChars + "]",
            RegexOptions.Compiled);

        private static readonly Regex StripInchRegex = new(
            @"\b\d+(?:\.\d+)?(?:[xX]\d+(?:\.\d+)?)?(?:/\d+)?\s*[" + InchQuoteChars + "]",
            RegexOptions.Compiled);

        private static readonly Regex StripPnRegex = new(
            @"\bPN\s*\d+(?:\.\d+)?\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex StripLengthRegex = new(
            @"\s*-\s*\d+m\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex CollapseWhitespaceRegex = new(
            @"\s+",
            RegexOptions.Compiled);

        /// <summary>
        /// Remove marcadores dimensionais (DN, mm, polegadas, PN, "- 6m"
        /// trailing) preservando a identificação do produto. Famílias Revit
        /// raramente carregam diâmetro no segment/typeName, então a entry
        /// do catálogo precisa estar "limpa" pra casar token-a-token.
        ///
        /// "Tubo Série Normal DN50 - 6m" → "Tubo Série Normal"
        /// "Bucha de Redução AQUATHERM 73x35mm" → "Bucha de Redução AQUATHERM"
        /// "Tubo PPR PN 20 50mm - 3m" → "Tubo PPR"
        /// </summary>
        public static string StripDimensions(string? text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            string s = text!;
            s = StripDnRegex.Replace(s, " ");
            s = StripMmRegex.Replace(s, " ");
            // Pré-strips de polegada precisam vir antes do strip simples
            // pra capturar pares como unidade (25x3/4", 2.1/2'x2').
            s = StripFractionInchPairRegex.Replace(s, " ");
            s = StripFractionInchRegex.Replace(s, " ");
            s = StripInchRegex.Replace(s, " ");
            s = StripPnRegex.Replace(s, " ");
            s = StripLengthRegex.Replace(s, " ");
            s = CollapseWhitespaceRegex.Replace(s, " ").Trim();
            return s;
        }

        public static string Normalize(string? text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            string decomposed = text!.Normalize(NormalizationForm.FormKD);
            StringBuilder sb = new StringBuilder(decomposed.Length);

            foreach (char ch in decomposed)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
                    continue;
                sb.Append(ch);
            }

            string s = sb.ToString().ToLowerInvariant();

            foreach (char sep in Separators)
                s = s.Replace(sep, ' ');

            while (s.IndexOf("  ", StringComparison.Ordinal) >= 0)
                s = s.Replace("  ", " ");

            return s.Trim();
        }

        /// <summary>
        /// Variante mais agressiva da normalização para uso em busca de UI:
        /// FormD (decompõe acentos), descarta marcas, troca qualquer caractere
        /// não alfanumérico por espaço, recompõe em FormC e colapsa espaços.
        /// Distinta de <see cref="Normalize"/> (que usa FormKD e preserva
        /// pontuação fora da lista de separadores).
        /// </summary>
        public static string NormalizeForSearch(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            string decomposed = value!
                .Trim()
                .ToLowerInvariant()
                .Normalize(NormalizationForm.FormD);

            StringBuilder builder = new StringBuilder(decomposed.Length);

            foreach (char ch in decomposed)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
                    continue;

                builder.Append(char.IsLetterOrDigit(ch) ? ch : ' ');
            }

            string compact = builder.ToString().Normalize(NormalizationForm.FormC);

            while (compact.IndexOf("  ", StringComparison.Ordinal) >= 0)
                compact = compact.Replace("  ", " ");

            return compact.Trim();
        }

        /// <summary>
        /// Verifica se <paramref name="text"/> contém <paramref name="needle"/>
        /// após normalização (lowercase, sem acentos, separadores → espaço).
        /// Devolve <c>false</c> quando o needle é vazio para evitar matches
        /// degenerados.
        /// </summary>
        public static bool ContainsNormalized(string? text, string? needle)
        {
            string a = Normalize(text);
            string b = Normalize(needle);
            return !string.IsNullOrEmpty(b) && a.IndexOf(b, StringComparison.Ordinal) >= 0;
        }

        public static IReadOnlyList<string> Tokenize(string? text)
        {
            string norm = Normalize(text);
            if (norm.Length == 0)
                return Array.Empty<string>();

            return norm
                .Split(WordSplit, StringSplitOptions.RemoveEmptyEntries)
                .ToList();
        }

        public static IReadOnlyList<string> CoreTokens(string? text, ISet<string> ignored)
        {
            return Tokenize(text)
                .Where(t => !ignored.Contains(t))
                .ToList();
        }

        public static bool ContainsAllTokens(string? text, IReadOnlyList<string> tokens)
        {
            HashSet<string> source = new HashSet<string>(Tokenize(text), StringComparer.Ordinal);
            return tokens.All(source.Contains);
        }

        public static bool ContainsAllCoreTokens(string? text, IReadOnlyList<string> coreTokens, ISet<string> ignored)
        {
            if (coreTokens.Count == 0)
                return false;

            HashSet<string> source = new HashSet<string>(CoreTokens(text, ignored), StringComparer.Ordinal);
            return coreTokens.All(source.Contains);
        }
    }
}
