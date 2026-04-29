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
        public const double DiameterToleranceMm = 0.5;

        public static readonly ISet<string> DefaultIgnoreTokens = new HashSet<string>(StringComparer.Ordinal)
        {
            "tubo", "tubos", "pipe", "pipes", "pvc", "material", "materiais",
            "marrom", "laranja", "branco", "branca", "azul", "verde", "cinza",
            "preto", "preta", "linha", "sistema",
        };

        private readonly IReadOnlyList<TigreCatalogEntry> _entries;
        private readonly ISet<string> _ignoreTokens;

        public TigreCatalog(IEnumerable<TigreRawCatalogRow> rows, ISet<string>? ignoreTokens = null)
        {
            if (rows == null) throw new ArgumentNullException(nameof(rows));

            _ignoreTokens = ignoreTokens ?? DefaultIgnoreTokens;

            _entries = rows
                .Where(r => !string.IsNullOrWhiteSpace(r.Description) && r.DiameterMm > 0 && r.Code > 0)
                .Select(r => new TigreCatalogEntry(r.Description, r.DiameterMm, r.Code, _ignoreTokens))
                .OrderByDescending(e => e.CoreTokens.Count)
                .ThenByDescending(e => e.Tokens.Count)
                .ToList();
        }

        public IReadOnlyList<TigreCatalogEntry> Entries => _entries;

        public TigreCatalogEntry? FindMatch(
            string descriptionText,
            string segmentText,
            string typeNameText,
            string combinedText,
            int diameterMmRound)
        {
            List<TigreCatalogEntry> sameDiameter = _entries
                .Where(e => Math.Abs(e.DiameterMm - diameterMmRound) <= DiameterToleranceMm)
                .ToList();

            if (sameDiameter.Count == 0)
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

        private TigreCatalogEntry? MatchByTokens(string text, List<TigreCatalogEntry> candidates, bool core)
        {
            foreach (TigreCatalogEntry entry in candidates)
            {
                IReadOnlyList<string> tokens = core ? entry.CoreTokens : entry.Tokens;
                if (tokens.Count == 0)
                    continue;

                if (core)
                {
                    if (TigreTextUtils.ContainsAllCoreTokens(text, tokens, _ignoreTokens))
                        return entry;
                }
                else
                {
                    if (TigreTextUtils.ContainsAllTokens(text, tokens))
                        return entry;
                }
            }

            return null;
        }
    }
}
