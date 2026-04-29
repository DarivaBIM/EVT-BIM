using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace DarivaBIM.Domain.Tigre
{
    public static class TigreTextUtils
    {
        private static readonly char[] Separators =
        {
            ';', ',', '.', '/', '\\', '-', '_', '(', ')', '[', ']', '{', '}', ':',
        };

        private static readonly char[] WordSplit = new[] { ' ' };

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
