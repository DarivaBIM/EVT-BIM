using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace FamiliesImporterHub.Infrastructure
{
    /// <summary>
    /// Catálogo Tigre (descrição/diâmetro/código) usado para preencher o
    /// parâmetro <c>Tigre: Código</c> dos tubos. Reproduz a lógica do script
    /// Dynamo: matching por tokens da descrição + diâmetro arredondado em mm.
    /// </summary>
    internal sealed class TigreCatalogEntry
    {
        public TigreCatalogEntry(string description, int diameterMm, int code)
        {
            DescriptionRaw = description;
            DescriptionNormalized = TigreTextUtils.Normalize(description);
            Tokens = TigreTextUtils.Tokenize(description);
            DiameterMm = diameterMm;
            Code = code;
        }

        public string DescriptionRaw { get; }
        public string DescriptionNormalized { get; }
        public IReadOnlyList<string> Tokens { get; }
        public int DiameterMm { get; }
        public int Code { get; }
    }

    internal static class TigreCodeCatalog
    {
        public const double DiameterToleranceMm = 0.5;

        // Linhas no formato (Descrição, Diâmetro nominal, Código).
        // Diâmetros em string aceitam mm (ex.: "40") ou polegadas em fração
        // (ex.: "3/4", "1.1/4"); a conversão para mm acontece no parse.
        private static readonly (string Description, string Diameter, int Code)[] RawRows = new[]
        {
            ("Série Reforçada", "40", 11054323),
            ("Série Reforçada", "50", 11054420),
            ("Série Reforçada", "75", 11054528),
            ("Série Reforçada", "100", 11055010),
            ("Série Reforçada", "150", 11051600),

            ("Série Normal", "40", 11111700),
            ("Série Normal", "50", 11030602),
            ("Série Normal", "75", 11030904),
            ("Série Normal", "100", 11031030),
            ("Série Normal", "150", 11031501),
            ("Série Normal", "200", 11032036),

            ("Redux", "40", 100002786),
            ("Redux", "50", 100002787),
            ("Redux", "75", 100002788),
            ("Redux", "100", 100002789),
            ("Redux", "150", 100002790),

            ("Soldável", "20", 10120209),
            ("Soldável", "25", 10120250),
            ("Soldável", "32", 10120322),
            ("Soldável", "40", 10120403),
            ("Soldável", "50", 10120500),
            ("Soldável", "60", 10120608),
            ("Soldável", "75", 10120756),
            ("Soldável", "85", 10120853),
            ("Soldável", "110", 10121035),

            ("ClicPEX Monocamada", "16", 300000774),
            ("ClicPEX Monocamada", "20", 300000775),
            ("ClicPEX Monocamada", "25", 300000776),
            ("ClicPEX Monocamada", "32", 300000777),

            ("Aquatherm", "15", 17000152),
            ("Aquatherm", "22", 17000225),
            ("Aquatherm", "28", 17000284),
            ("Aquatherm", "35", 17001086),
            ("Aquatherm", "42", 17001108),
            ("Aquatherm", "54", 17001132),
            ("Aquatherm", "73", 17001515),
            ("Aquatherm", "89", 17001531),
            ("Aquatherm", "114", 17001558),

            ("CPVC TIGREFire", "3/4", 17020056),
            ("CPVC TIGREFire", "1", 17020080),
            ("CPVC TIGREFire", "1.1/4", 17020110),
            ("CPVC TIGREFire", "1.1/2", 17020153),
            ("CPVC TIGREFire", "2", 17020188),
            ("CPVC TIGREFire", "2.1/2", 17020226),
            ("CPVC TIGREFire", "3", 17020250),

            ("PPR PN12.5", "32", 17010565),
            ("PPR PN12.5", "40", 17010581),
            ("PPR PN12.5", "50", 17010603),
            ("PPR PN12.5", "63", 17020620),
            ("PPR PN12.5", "75", 17010646),
            ("PPR PN12.5", "90", 17010670),
            ("PPR PN12.5", "110", 17010689),

            ("PPR PN20", "20", 17010026),
            ("PPR PN20", "25", 17010042),
            ("PPR PN20", "32", 17010069),
            ("PPR PN20", "40", 17010085),
            ("PPR PN20", "50", 17010107),
            ("PPR PN20", "63", 17010123),
            ("PPR PN20", "75", 17010140),
            ("PPR PN20", "90", 17010174),
            ("PPR PN20", "110", 17010182),

            ("PPR PN25", "20", 17010328),
            ("PPR PN25", "25", 17010344),
            ("PPR PN25", "32", 17010360),
            ("PPR PN25", "40", 17010387),
            ("PPR PN25", "50", 17010409),
            ("PPR PN25", "63", 17010425),
            ("PPR PN25", "75", 17010441),
            ("PPR PN25", "90", 17010476),
        };

        private static IReadOnlyList<TigreCatalogEntry>? _entries;

        public static IReadOnlyList<TigreCatalogEntry> Entries
        {
            get
            {
                if (_entries == null)
                {
                    List<TigreCatalogEntry> built = new();

                    foreach ((string desc, string dia, int code) in RawRows)
                    {
                        if (!TryParseDiameterMm(dia, out int diaMm))
                            continue;

                        built.Add(new TigreCatalogEntry(desc, diaMm, code));
                    }

                    // Tokens mais específicos primeiro (ex.: "PPR PN12.5" antes de "PPR")
                    // para que o match não pare em uma descrição mais genérica.
                    _entries = built
                        .OrderByDescending(e => e.Tokens.Count)
                        .ToList();
                }

                return _entries;
            }
        }

        public static TigreCatalogEntry? FindMatch(string combinedNormalizedText, int diameterMmRound)
        {
            foreach (TigreCatalogEntry entry in Entries)
            {
                if (Math.Abs(entry.DiameterMm - diameterMmRound) > DiameterToleranceMm)
                    continue;

                bool ok = true;
                foreach (string token in entry.Tokens)
                {
                    if (combinedNormalizedText.IndexOf(token, StringComparison.Ordinal) < 0)
                    {
                        ok = false;
                        break;
                    }
                }

                if (ok)
                    return entry;
            }

            return null;
        }

        private static bool TryParseDiameterMm(string value, out int diameterMm)
        {
            diameterMm = 0;
            if (string.IsNullOrWhiteSpace(value))
                return false;

            string text = value.Trim().Replace(",", ".");

            if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int asInt))
            {
                diameterMm = asInt;
                return true;
            }

            if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double asDouble))
            {
                diameterMm = (int)Math.Round(asDouble);
                return true;
            }

            // Polegadas em fração (ex.: "3/4", "1.1/4", "2.1/2") — converte para mm.
            if (TryParseInchFraction(text, out double inches))
            {
                diameterMm = (int)Math.Round(inches * 25.4);
                return true;
            }

            return false;
        }

        private static bool TryParseInchFraction(string text, out double inches)
        {
            inches = 0;

            int slash = text.IndexOf('/');
            if (slash < 0)
                return false;

            string left = text.Substring(0, slash);
            string right = text.Substring(slash + 1);

            // "3/4" → 3/4; "1.1/4" → 1 + 1/4 (o ponto separa inteiro e fração).
            int dot = left.LastIndexOf('.');
            double whole = 0;
            string numeratorText = left;

            if (dot >= 0)
            {
                if (!double.TryParse(left.Substring(0, dot), NumberStyles.Float, CultureInfo.InvariantCulture, out whole))
                    return false;

                numeratorText = left.Substring(dot + 1);
            }

            if (!double.TryParse(numeratorText, NumberStyles.Float, CultureInfo.InvariantCulture, out double numerator))
                return false;

            if (!double.TryParse(right, NumberStyles.Float, CultureInfo.InvariantCulture, out double denominator) || denominator == 0)
                return false;

            inches = whole + numerator / denominator;
            return true;
        }
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

        public static IReadOnlyList<string> Tokenize(string text)
        {
            string norm = Normalize(text);
            if (norm.Length == 0)
                return Array.Empty<string>();

            return norm
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .ToList();
        }
    }
}
