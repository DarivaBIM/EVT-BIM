using System;
using System.Collections.Generic;
using System.Linq;
using DarivaBIM.Application.DTOs.Family;
using DarivaBIM.Domain.Tigre;

namespace DarivaBIM.Plugin.Ui.Search
{
    /// <summary>
    /// Funções puras de indexação e matching usadas pela busca incremental
    /// da galeria de famílias. Antes viviam embutidas em
    /// <c>FamiliesPage.xaml.cs</c>; ficaram aqui para ser testáveis em
    /// isolamento e para reduzir a superfície da Page.
    /// </summary>
    internal static class FamilySearchHelpers
    {
        public static string NormalizeForSearch(string value) =>
            TigreTextUtils.NormalizeForSearch(value);

        public static string Compact(string value) =>
            string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Replace(" ", string.Empty);

        public static IEnumerable<string> Tokenize(string value) =>
            value
                .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(token => !string.IsNullOrWhiteSpace(token))
                .Distinct(StringComparer.Ordinal);

        public static string BuildSearchIndex(FamilyItem family)
        {
            var parts = new List<string>
            {
                family.Name,
                family.FileName,
                family.ManufacturerName,
            };

            if (family.Keywords != null)
            {
                parts.AddRange(family.Keywords);
            }

            if (family.Tags != null)
            {
                parts.AddRange(
                    family.Tags
                        .Where(tag => tag != null && !string.IsNullOrWhiteSpace(tag.Description))
                        .Select(tag => tag.Description));
            }

            return NormalizeForSearch(
                string.Join(" ", parts.Where(part => !string.IsNullOrWhiteSpace(part))));
        }

        public static bool MatchesFast(
            FamilyItem family,
            string normalizedSearch,
            string compactSearch,
            IReadOnlyList<string> searchTokens)
        {
            if (string.IsNullOrWhiteSpace(family.SearchIndex))
            {
                return false;
            }

            if (family.SearchIndex.Contains(normalizedSearch, StringComparison.Ordinal))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(compactSearch) &&
                family.SearchIndexCompact.Contains(compactSearch, StringComparison.Ordinal))
            {
                return true;
            }

            for (int i = 0; i < searchTokens.Count; i++)
            {
                string token = searchTokens[i];

                bool tokenMatched =
                    family.SearchIndex.Contains(token, StringComparison.Ordinal) ||
                    family.SearchIndexCompact.Contains(token, StringComparison.Ordinal);

                if (!tokenMatched)
                {
                    return false;
                }
            }

            return searchTokens.Count > 0;
        }

        // Casamento OR — uma família passa o filtro se pertence a QUALQUER
        // sistema marcado. Antes era AND, o que rapidamente zerava a galeria
        // quando o usuário marcava 2+ chips.
        public static bool MatchesSistemas(
            FamilyItem family,
            IReadOnlyCollection<string> selectedSistemaIds,
            IReadOnlyDictionary<int, IReadOnlyList<string>> familySistemas)
        {
            if (selectedSistemaIds.Count == 0)
            {
                return true;
            }

            if (!familySistemas.TryGetValue(family.Id, out IReadOnlyList<string>? sistemaIds) ||
                sistemaIds.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < sistemaIds.Count; i++)
            {
                if (selectedSistemaIds.Contains(sistemaIds[i]))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
