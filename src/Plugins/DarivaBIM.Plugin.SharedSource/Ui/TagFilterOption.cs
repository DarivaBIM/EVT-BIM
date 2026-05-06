using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DarivaBIM.Plugin.Ui
{
    /// <summary>
    /// View-model for a single tag chip. Two-way bound to a styled
    /// <c>ToggleButton.IsChecked</c>; <see cref="FamiliesPage"/> subscribes to
    /// <see cref="INotifyPropertyChanged"/> to rerun filtering whenever the
    /// user toggles a chip on or off.
    ///
    /// Color/icon properties mirror the per-system palette used by
    /// <c>TagBadge</c> on the family cards, so the filter dots and the badges
    /// share a single visual language: a blue dot here = the blue badge on
    /// "Água Fria" cards.
    /// </summary>
    public sealed class TagFilterOption : INotifyPropertyChanged
    {
        // Filter chip PNGs live next to the plugin assembly, mirroring the
        // ribbon icons. Loading happens via Assembly.Location + Path.Combine
        // so the same code path works for V2025 and V2026 without WPF
        // pack-uri assembly-name juggling.
        private const string FilterIconsRelativeFolder = "Ribbon\\Resources\\FilterIcons";

        // Single shared cache: each PNG is decoded once per process, then
        // every TagFilterOption referencing the same key reuses the frozen
        // BitmapImage. Missing files cache as null so we don't re-stat them.
        private static readonly Dictionary<string, BitmapImage?> IconCache =
            new(StringComparer.OrdinalIgnoreCase);

        private bool _isSelected;

        public TagFilterOption(string description, string key)
        {
            Description = description;
            Key = key;

            var palette = ResolvePalette(description);

            BackgroundBrush = palette.Background;
            ForegroundBrush = palette.Foreground;
            Glyph = palette.Glyph;
            Icon = LoadIcon(palette.IconFileName);
        }

        public string Description { get; }

        // Normalized description (lowercase, no diacritics) used for matching
        // against family tags. Stored once at construction so filtering doesn't
        // pay normalization cost per family.
        public string Key { get; }

        // Pastel category color used as the chip background in every state
        // — selection is signaled by a colored stroke around it (the
        // <see cref="ForegroundBrush"/>), not by swapping the fill, so the
        // chip's identity stays readable whether it's checked or not.
        public Brush BackgroundBrush { get; }

        public Brush ForegroundBrush { get; }

        // Single Segoe MDL2 Assets glyph that visually hints at the system
        // (water drop, flame, cloud, etc.). Falls back to the description's
        // first letter if the system isn't in the curated palette. Rendered
        // only when <see cref="Icon"/> is null (no PNG dropped in
        // Resources/FilterIcons).
        public string Glyph { get; }

        // Custom PNG icon for the chip. When non-null the WPF template
        // renders an <Image> bound to this; when null it falls back to the
        // Segoe MDL2 glyph above. Drop a PNG named after the palette's
        // IconFileName into Plugin.SharedSource/Resources/FilterIcons to
        // light this up — see the README in that folder.
        public BitmapImage? Icon { get; }

        public bool HasIcon => Icon != null;

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value)
                {
                    return;
                }

                _isSelected = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private readonly struct Palette
        {
            public Palette(string background, string foreground, string glyph, string? iconFileName)
            {
                Background = CreateFrozenBrush(background);
                Foreground = CreateFrozenBrush(foreground);
                Glyph = glyph;
                IconFileName = iconFileName;
            }

            public SolidColorBrush Background { get; }
            public SolidColorBrush Foreground { get; }
            public string Glyph { get; }
            public string? IconFileName { get; }
        }

        private static SolidColorBrush CreateFrozenBrush(string hex)
        {
            SolidColorBrush brush = (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
            brush.Freeze();
            return brush;
        }

        private static Palette ResolvePalette(string description)
        {
            string key = NormalizeKey(description);

            // Background HEX is the *checked-state* pastel chosen by the
            // user (lighter than the previous Material 50 tints, so the
            // PNG icon stays the focal element). Foreground HEX is the
            // category accent — used by the Segoe MDL2 fallback glyph and
            // available to any future text/badge that wants the brand
            // color of the system. Backgrounds and foregrounds documented
            // in Resources/FilterIcons/README.md.
            //
            // Glyphs are Segoe MDL2 Assets code points, kept as fallback for
            // when the PNG file isn't present yet — picked for shape
            // recognizability at 14–16px; tooltip carries the literal system
            // name so users never depend on glyph reading.
            switch (key)
            {
                case "agua fria":
                    return new Palette("#EEF6FF", "#1565C0", "", "agua_fria.png");
                case "agua quente":
                    return new Palette("#FDEEEE", "#D84343", "", "agua_quente.png");
                case "esgoto":
                    return new Palette("#EEF8EF", "#2E7D32", "", "esgoto.png");
                case "pluvial":
                    return new Palette("#F0F0FF", "#5E60CE", "", "pluvial.png");
                case "caixas e ralos":
                    return new Palette("#F0F5F7", "#546E7A", "", "caixas_e_ralos.png");
                case "reservatorio":
                    return new Palette("#EAFBFF", "#0E7490", "", "reservatorio.png");
                case "sted":
                    return new Palette("#DCFCE7", "#15803D", "", null);
                case "piscina":
                    return new Palette("#EDF9FF", "#039BE5", "", "piscina.png");
                case "irrigacao":
                    return new Palette("#F5FAEA", "#6B8E23", "", "irrigacao.png");
                case "poco":
                    return new Palette("#FFF8E6", "#C88719", "", "poco.png");
                case "bombas":
                    return new Palette("#FFF1E6", "#EF6C00", "", "bombas.png");
                case "valvula":
                    return new Palette("#EAF9F7", "#00796B", "", "valvula.png");
                case "utilitario":
                case "ponto de utilizacao":
                    return new Palette("#F7F7F7", "#616161", "", "ponto_de_utilizacao.png");
                case "combate a incendio":
                    return new Palette("#FCEAEA", "#B71C1C", "", "combate_a_incendio.png");
                case "tratamento de esgoto":
                    return new Palette("#F7F1EE", "#6D4C41", "", "tratamento_de_esgoto.png");
                default:
                    return new Palette(
                        "#EEF2FF",
                        "#4338CA",
                        FirstLetterGlyph(description),
                        null);
            }
        }

        private static BitmapImage? LoadIcon(string? fileName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                return null;
            }

            if (IconCache.TryGetValue(fileName, out BitmapImage? cached))
            {
                return cached;
            }

            BitmapImage? image = null;
            try
            {
                string assemblyLocation = typeof(TagFilterOption).Assembly.Location;
                string baseDir = Path.GetDirectoryName(assemblyLocation) ?? string.Empty;
                string fullPath = Path.Combine(baseDir, FilterIconsRelativeFolder, fileName!);

                if (File.Exists(fullPath))
                {
                    image = new BitmapImage();
                    image.BeginInit();
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    // 28px chip * 2 covers HiDPI without holding a full-resolution decode.
                    image.DecodePixelWidth = 56;
                    image.UriSource = new Uri(fullPath, UriKind.Absolute);
                    image.EndInit();
                    image.Freeze();
                }
            }
            catch
            {
                // Missing/corrupt PNG falls back to the Segoe MDL2 glyph —
                // surfacing the failure to the user adds nothing actionable.
                image = null;
            }

            IconCache[fileName] = image;
            return image;
        }

        private static string FirstLetterGlyph(string description)
        {
            if (string.IsNullOrWhiteSpace(description))
            {
                return "?";
            }

            string trimmed = description.Trim();
            return trimmed.Substring(0, 1).ToUpperInvariant();
        }

        private static string NormalizeKey(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            string normalized = value
                .Trim()
                .ToLowerInvariant()
                .Normalize(NormalizationForm.FormD);

            var builder = new StringBuilder();

            foreach (char c in normalized)
            {
                UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(c);

                if (category == UnicodeCategory.NonSpacingMark)
                {
                    continue;
                }

                builder.Append(char.IsLetterOrDigit(c) ? c : ' ');
            }

            return builder
                .ToString()
                .Normalize(NormalizationForm.FormC)
                .Replace("  ", " ")
                .Trim();
        }
    }
}
