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
        private readonly HashSet<int> _allCodes;
        private readonly ISet<string> _ignoreTokens;

        public TigreCatalog(IEnumerable<TigreRawCatalogRow> rows, ISet<string>? ignoreTokens = null)
        {
            if (rows == null) throw new ArgumentNullException(nameof(rows));

            _ignoreTokens = ignoreTokens ?? DefaultIgnoreTokens;

            _entries = rows
                .Where(r => !string.IsNullOrWhiteSpace(r.Description) && r.DiameterMm > 0 && r.Code > 0)
                .Select(r => new TigreCatalogEntry(r, _ignoreTokens))
                .OrderByDescending(e => e.LeanCoreTokens.Count)
                .ThenByDescending(e => e.LeanTokens.Count)
                .ToList();

            // Indexa por diâmetro (int) para eliminar o filtro O(n) por chamada
            // de FindMatch. Como DiameterMm é int e a tolerância (0.5) só casa
            // em igualdade exata, o dicionário preserva a semântica do filtro.
            _entriesByDiameter = _entries
                .GroupBy(e => e.DiameterMm)
                .ToDictionary(g => g.Key, g => (IReadOnlyList<TigreCatalogEntry>)g.ToList());

            // Set de todos os codes pra HasCode O(1). Usado pelo
            // TigreDetectionRules (Slice 2C) pra validar Tigre: Código
            // pré-existente em elemento — Sinal 0 trumpa veto Manufacturer.
            _allCodes = new HashSet<int>(_entries.Select(e => e.Code));
        }

        public IReadOnlyList<TigreCatalogEntry> Entries => _entries;

        /// <summary>
        /// True quando <paramref name="code"/> é positivo e existe em
        /// alguma entry do catálogo. Lookup O(1). Code 0 = ausente.
        /// </summary>
        public bool HasCode(int code) => code > 0 && _allCodes.Contains(code);

        public TigreCatalogEntry? FindMatch(
            string descriptionText,
            string segmentText,
            string typeNameText,
            string combinedText,
            int diameterMmRound)
            => FindMatch(
                descriptionText, segmentText, typeNameText, combinedText,
                diameterMmRound, kindFilter: (string?)null);

        /// <summary>
        /// Overload com filtro de kind ("pipe", "fitting", etc.). Quando
        /// não-nulo, restringe candidates às entries onde
        /// <see cref="TigreCatalogEntry.Kind"/> == kindFilter (compare
        /// ordinal case-insensitive). kindFilter desconhecido (sem entries)
        /// retorna null sem chamar o matcher. Delega pro overload de
        /// conjunto (Slice 3.6).
        /// </summary>
        public TigreCatalogEntry? FindMatch(
            string descriptionText,
            string segmentText,
            string typeNameText,
            string combinedText,
            int diameterMmRound,
            string? kindFilter)
            => FindMatch(
                descriptionText, segmentText, typeNameText, combinedText,
                diameterMmRound,
                kindFilters: kindFilter != null ? new[] { kindFilter } : null);

        /// <summary>
        /// Overload com conjunto de kinds aceitáveis (Slice 3.6). Resolve
        /// o gap onde uma BIC Revit cobre N kinds do catálogo: PipeFitting
        /// engloba fitting/tee/elbow/reducer/cap. Sem isso, 55% do catálogo
        /// (479 SKUs com kind ≠ "fitting") ficava inacessível ao Applier
        /// quando data.Kind passava "fitting" puro.
        ///
        /// Match passa se <see cref="TigreCatalogEntry.Kind"/> ∈
        /// <paramref name="kindFilters"/> (case-insensitive). Conjunto nulo
        /// ou vazio = sem filtro. Filtro desconhecido (nenhuma entry casa
        /// no diâmetro+kinds) retorna null sem matcher.
        /// </summary>
        public TigreCatalogEntry? FindMatch(
            string descriptionText,
            string segmentText,
            string typeNameText,
            string combinedText,
            int diameterMmRound,
            IReadOnlyCollection<string>? kindFilters)
        {
            if (!_entriesByDiameter.TryGetValue(diameterMmRound, out IReadOnlyList<TigreCatalogEntry>? sameDiameter))
                return null;

            // PN extraction: se a query menciona "PN 20"/"PN12.5"/etc, só
            // entries com mesma classe podem casar — desambigua PPR
            // PN12.5/PN20/PN25 que colapsariam pro mesmo lean. Primeira
            // ocorrência no combined > description > typeName > segment.
            string? queryPn =
                TigreTextUtils.ExtractPn(combinedText) ??
                TigreTextUtils.ExtractPn(descriptionText) ??
                TigreTextUtils.ExtractPn(typeNameText) ??
                TigreTextUtils.ExtractPn(segmentText);
            if (queryPn != null)
            {
                List<TigreCatalogEntry> withPn = sameDiameter
                    .Where(e => string.Equals(e.Pn, queryPn, StringComparison.Ordinal))
                    .ToList();
                if (withPn.Count == 0)
                    return null;
                sameDiameter = withPn;
            }

            if (kindFilters != null && kindFilters.Count > 0)
            {
                HashSet<string> kindSet = new HashSet<string>(
                    kindFilters, StringComparer.OrdinalIgnoreCase);
                List<TigreCatalogEntry> filtered = sameDiameter
                    .Where(e => e.Kind != null && kindSet.Contains(e.Kind))
                    .ToList();
                if (filtered.Count == 0)
                    return null;
                sameDiameter = filtered;
            }

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
