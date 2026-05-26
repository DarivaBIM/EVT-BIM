using System;
using System.Collections.Generic;
using System.Linq;

namespace DarivaBIM.Domain.Tigre
{
    /// <summary>
    /// In-memory Tigre catalog. Holds a sorted set of <see cref="TigreCatalogEntry"/> built
    /// from a list of raw rows and exposes the matching algorithm previously
    /// embedded in TigreCodeCatalog.
    /// </summary>
    public sealed class TigreCatalog
    {
        public static readonly ISet<string> DefaultIgnoreTokens = new HashSet<string>(StringComparer.Ordinal)
        {
            "tubo", "tubos", "pipe", "pipes", "pvc", "material", "materiais",
            "marrom", "laranja", "branco", "branca", "azul", "verde", "cinza",
            "preto", "preta", "linha", "sistema",
            // "x" sobra como resíduo de pares dimensionais que o
            // StripInch não consegue colar 100% (ex.: "2.1/2'x2'"
            // vira "x" no lean depois de strippar os dois termos).
            "x",
        };

        private readonly IReadOnlyList<TigreCatalogEntry> _entries;
        private readonly Dictionary<int, IReadOnlyList<TigreCatalogEntry>> _entriesByDiameter;
        private readonly ISet<string> _ignoreTokens;

        public TigreCatalog(IEnumerable<TigreRawCatalogRow> rows, ISet<string>? ignoreTokens = null)
        {
            if (rows == null) throw new ArgumentNullException(nameof(rows));

            _ignoreTokens = ignoreTokens ?? DefaultIgnoreTokens;

            _entries = rows
                .Where(r => !string.IsNullOrWhiteSpace(r.Description) && r.DiameterMm > 0 && r.Code > 0)
                .Select(r => new TigreCatalogEntry(r.Description, r.DiameterMm, r.Code, _ignoreTokens))
                .OrderByDescending(e => e.LeanCoreTokens.Count)
                .ThenByDescending(e => e.LeanTokens.Count)
                .ToList();

            // Indexa por diâmetro (int) para eliminar o filtro O(n) por chamada
            // de FindMatch. Como DiameterMm é int e a tolerância (0.5) só casa
            // em igualdade exata, o dicionário preserva a semântica do filtro.
            _entriesByDiameter = _entries
                .GroupBy(e => e.DiameterMm)
                .ToDictionary(g => g.Key, g => (IReadOnlyList<TigreCatalogEntry>)g.ToList());
        }

        public IReadOnlyList<TigreCatalogEntry> Entries => _entries;

        public TigreCatalogEntry? FindMatch(
            string descriptionText,
            string segmentText,
            string typeNameText,
            string combinedText,
            int diameterMmRound)
        {
            if (!_entriesByDiameter.TryGetValue(diameterMmRound, out IReadOnlyList<TigreCatalogEntry>? sameDiameter))
                return null;

            return
                MatchByTokens(descriptionText, sameDiameter, core: false) ??
                MatchByTokens(descriptionText, sameDiameter, core: true) ??
                MatchByTokens(typeNameText, sameDiameter, core: false) ??
                MatchByTokens(typeNameText, sameDiameter, core: true) ??
                MatchByTokens(segmentText, sameDiameter, core: false) ??
                MatchByTokens(segmentText, sameDiameter, core: true) ??
                MatchByTokens(combinedText, sameDiameter, core: false) ??
                MatchByTokens(combinedText, sameDiameter, core: true);
        }

        private TigreCatalogEntry? MatchByTokens(string text, IReadOnlyList<TigreCatalogEntry> candidates, bool core)
        {
            // Tokeniza contra LeanCoreTokens/LeanTokens (descrição sem
            // marcadores dimensionais) — diâmetro já foi pre-filtrado em
            // FindMatch via _entriesByDiameter. Famílias Revit não carregam
            // DN/mm/comprimento no segment, então comparar tokens raw da
            // descrição completa falha em ~todas entries do catálogo novo.
            //
            // AmbiguityGuard: coleta TODAS as entries que casam no tier,
            // em vez de retornar a primeira. Tie-break = especificidade
            // (mais tokens lean = mais específica). Empate no topo
            // (várias entries com mesmo número de tokens, mesmo lean
            // após strip) → retorna null. PipeCodes deixa código vazio
            // e o audit do Quantifica reclama, em vez de gravar SKU
            // arbitrário escolhido pela ordem de leitura do JSON.
            List<TigreCatalogEntry> matches = new();
            foreach (TigreCatalogEntry entry in candidates)
            {
                IReadOnlyList<string> tokens = core ? entry.LeanCoreTokens : entry.LeanTokens;
                if (tokens.Count == 0)
                    continue;

                bool isMatch = core
                    ? TigreTextUtils.ContainsAllCoreTokens(text, tokens, _ignoreTokens)
                    : TigreTextUtils.ContainsAllTokens(text, tokens);

                if (isMatch)
                    matches.Add(entry);
            }

            if (matches.Count == 0) return null;
            if (matches.Count == 1) return matches[0];

            int maxTokens = matches.Max(e =>
                (core ? e.LeanCoreTokens : e.LeanTokens).Count);
            List<TigreCatalogEntry> mostSpecific = matches
                .Where(e =>
                    (core ? e.LeanCoreTokens : e.LeanTokens).Count == maxTokens)
                .ToList();
            return mostSpecific.Count == 1 ? mostSpecific[0] : null;
        }
    }
}
