using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace FamiliesImporterHub.Infrastructure
{
    internal sealed class TigreCatalogEntry
    {
        public TigreCatalogEntry(string description, int diameterMm, int code, IReadOnlySet<string> ignoreTokens)
        {
            DescriptionRaw = description;
            Tokens = TigreTextUtils.Tokenize(description);
            CoreTokens = TigreTextUtils.CoreTokens(description, ignoreTokens);
            if (CoreTokens.Count == 0)
                CoreTokens = Tokens;

            DiameterMm = diameterMm;
            Code = code;
        }

        public string DescriptionRaw { get; }
        public IReadOnlyList<string> Tokens { get; }
        public IReadOnlyList<string> CoreTokens { get; private set; }
        public int DiameterMm { get; }
        public int Code { get; }
    }

    internal sealed class TigreRawCatalogRow
    {
        public string Description { get; set; } = string.Empty;
        public int DiameterMm { get; set; }
        public int Code { get; set; }
    }

    internal static class TigreCodeCatalog
    {
        public const double DiameterToleranceMm = 0.5;

        private static readonly string CatalogJsonPath =
            Path.Combine(AppContext.BaseDirectory, "Data", "tigre_codes.json");

        private static readonly HashSet<string> IgnoreTokens = new(StringComparer.Ordinal)
        {
            "tubo", "tubos", "pipe", "pipes", "pvc", "material", "materiais",
            "marrom", "laranja", "branco", "branca", "azul", "verde", "cinza",
            "preto", "preta", "linha", "sistema",
        };

        private static readonly (string Description, int DiameterMm, int Code)[] EmbeddedFallbackRows =
        {
            ("Série Reforçada", 40, 11054323),
            ("Série Reforçada", 50, 11054420),
            ("Série Reforçada", 75, 11054528),
            ("Série Reforçada", 100, 11055010),
            ("Série Reforçada", 150, 11051600),

            ("Série Normal", 40, 11111700),
            ("Série Normal", 50, 11030602),
            ("Série Normal", 75, 11030904),
            ("Série Normal", 100, 11031030),
            ("Série Normal", 150, 11031501),
            ("Série Normal", 200, 11032036),

            ("Redux Laranja", 40, 100002786),
            ("Redux Laranja", 50, 100002787),
            ("Redux Laranja", 75, 100002788),
            ("Redux Laranja", 100, 100002789),
            ("Redux Laranja", 150, 100002790),

            ("Soldável Marrom", 20, 10120209),
            ("Soldável Marrom", 25, 10120250),
            ("Soldável Marrom", 32, 10120322),
            ("Soldável Marrom", 40, 10120403),
            ("Soldável Marrom", 50, 10120500),
            ("Soldável Marrom", 60, 10120608),
            ("Soldável Marrom", 75, 10120756),
            ("Soldável Marrom", 85, 10120853),
            ("Soldável Marrom", 110, 10121035),

            ("ClicPEX Monocamada", 16, 300000774),
            ("ClicPEX Monocamada", 20, 300000775),
            ("ClicPEX Monocamada", 25, 300000776),
            ("ClicPEX Monocamada", 32, 300000777),

            ("Aquatherm", 15, 17000152),
            ("Aquatherm", 22, 17000225),
            ("Aquatherm", 28, 17000284),
            ("Aquatherm", 35, 17001086),
            ("Aquatherm", 42, 17001108),
            ("Aquatherm", 54, 17001132),
            ("Aquatherm", 73, 17001515),
            ("Aquatherm", 89, 17001531),
            ("Aquatherm", 114, 17001558),

            ("CPVC TIGREFire", 25, 17020056),
            ("CPVC TIGREFire", 32, 17020080),
            ("CPVC TIGREFire", 40, 17020110),
            ("CPVC TIGREFire", 50, 17020153),
            ("CPVC TIGREFire", 60, 17020188),
            ("CPVC TIGREFire", 75, 17020226),
            ("CPVC TIGREFire", 85, 17020250),

            ("PPR PN12", 32, 17010565),
            ("PPR PN12", 40, 17010581),
            ("PPR PN12", 50, 17010603),
            ("PPR PN12", 63, 17020620),
            ("PPR PN12", 75, 17010646),
            ("PPR PN12", 90, 17010670),
            ("PPR PN12", 110, 17010689),

            ("PPR PN20", 20, 17010026),
            ("PPR PN20", 25, 17010042),
            ("PPR PN20", 32, 17010069),
            ("PPR PN20", 40, 17010085),
            ("PPR PN20", 50, 17010107),
            ("PPR PN20", 63, 17010123),
            ("PPR PN20", 75, 17010140),
            ("PPR PN20", 90, 17010174),
            ("PPR PN20", 110, 17010182),

            ("PPR PN25", 20, 17010328),
            ("PPR PN25", 25, 17010344),
            ("PPR PN25", 32, 17010360),
            ("PPR PN25", 40, 17010387),
            ("PPR PN25", 50, 17010409),
            ("PPR PN25", 63, 17010425),
            ("PPR PN25", 75, 17010441),
            ("PPR PN25", 90, 17010476),
        };

        private static IReadOnlyList<TigreCatalogEntry>? _entries;

        public static IReadOnlyList<TigreCatalogEntry> Entries
        {
            get
            {
                if (_entries == null)
                {
                    IEnumerable<TigreRawCatalogRow> rows = LoadRowsFromJson(CatalogJsonPath) ?? BuildFallbackRows();

                    List<TigreCatalogEntry> built = rows
                        .Where(r => !string.IsNullOrWhiteSpace(r.Description) && r.DiameterMm > 0 && r.Code > 0)
                        .Select(r => new TigreCatalogEntry(r.Description, r.DiameterMm, r.Code, IgnoreTokens))
                        .OrderByDescending(e => e.CoreTokens.Count)
                        .ThenByDescending(e => e.Tokens.Count)
                        .ToList();

                    _entries = built;
                }

                return _entries;
            }
        }

        public static TigreCatalogEntry? FindMatch(
            string descriptionText,
            string segmentText,
            string typeNameText,
            string combinedText,
            int diameterMmRound)
        {
            List<TigreCatalogEntry> sameDiameter = Entries
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

        private static TigreCatalogEntry? MatchByTokens(string text, List<TigreCatalogEntry> candidates, bool core)
        {
            foreach (TigreCatalogEntry entry in candidates)
            {
                IReadOnlyList<string> tokens = core ? entry.CoreTokens : entry.Tokens;
                if (tokens.Count == 0)
                    continue;

                if (core)
                {
                    if (TigreTextUtils.ContainsAllCoreTokens(text, tokens, IgnoreTokens))
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

        private static IEnumerable<TigreRawCatalogRow>? LoadRowsFromJson(string jsonPath)
        {
            try
            {
                if (!File.Exists(jsonPath))
                    return null;

                string json = File.ReadAllText(jsonPath, Encoding.UTF8);
                List<TigreRawCatalogRow>? rows = JsonSerializer.Deserialize<List<TigreRawCatalogRow>>(json);
                return rows;
            }
            catch
            {
                return null;
            }
        }

        private static IEnumerable<TigreRawCatalogRow> BuildFallbackRows() =>
            EmbeddedFallbackRows.Select(r => new TigreRawCatalogRow
            {
                Description = r.Description,
                DiameterMm = r.DiameterMm,
                Code = r.Code,
            });
    }

    internal static class TigreTextUtils
    {
        private static readonly char[] Separators =
        {
            ';', ',', '.', '/', '\\', '-', '_', '(', ')', '[', ']', '{', '}', ':',
        };

        public static string Normalize(string? text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            string decomposed = text.Normalize(NormalizationForm.FormKD);
            StringBuilder sb = new(decomposed.Length);

            foreach (char ch in decomposed)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
                    continue;
                sb.Append(ch);
            }

            string s = sb.ToString().ToLowerInvariant();

            foreach (char sep in Separators)
                s = s.Replace(sep, ' ');

            while (s.Contains("  "))
                s = s.Replace("  ", " ");

            return s.Trim();
        }

        public static IReadOnlyList<string> Tokenize(string? text)
        {
            string norm = Normalize(text);
            if (norm.Length == 0)
                return Array.Empty<string>();

            return norm
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .ToList();
        }

        public static IReadOnlyList<string> CoreTokens(string? text, IReadOnlySet<string> ignored)
        {
            return Tokenize(text)
                .Where(t => !ignored.Contains(t))
                .ToList();
        }

        public static bool ContainsAllTokens(string? text, IReadOnlyList<string> tokens)
        {
            HashSet<string> source = Tokenize(text).ToHashSet(StringComparer.Ordinal);
            return tokens.All(source.Contains);
        }

        public static bool ContainsAllCoreTokens(string? text, IReadOnlyList<string> coreTokens, IReadOnlySet<string> ignored)
        {
            if (coreTokens.Count == 0)
                return false;

            HashSet<string> source = CoreTokens(text, ignored).ToHashSet(StringComparer.Ordinal);
            return coreTokens.All(source.Contains);
        }
    }
}
