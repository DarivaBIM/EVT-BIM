using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Media;

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
        private bool _isSelected;

        public TagFilterOption(string description, string key)
        {
            Description = description;
            Key = key;

            var palette = ResolvePalette(description);

            BackgroundBrush = palette.Background;
            ForegroundBrush = palette.Foreground;
            Glyph = palette.Glyph;
        }

        public string Description { get; }

        // Normalized description (lowercase, no diacritics) used for matching
        // against family tags. Stored once at construction so filtering doesn't
        // pay normalization cost per family.
        public string Key { get; }

        public Brush BackgroundBrush { get; }

        public Brush ForegroundBrush { get; }

        // Single Segoe MDL2 Assets glyph that visually hints at the system
        // (water drop, flame, cloud, etc.). Falls back to the description's
        // first letter if the system isn't in the curated palette.
        public string Glyph { get; }

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
            public Palette(string background, string foreground, string glyph)
            {
                Background = (SolidColorBrush)new BrushConverter().ConvertFromString(background)!;
                Foreground = (SolidColorBrush)new BrushConverter().ConvertFromString(foreground)!;
                Background.Freeze();
                Foreground.Freeze();
                Glyph = glyph;
            }

            public SolidColorBrush Background { get; }
            public SolidColorBrush Foreground { get; }
            public string Glyph { get; }
        }

        private static Palette ResolvePalette(string description)
        {
            string key = NormalizeKey(description);

            // Glyphs are Segoe MDL2 Assets code points (Windows 10+). Picked
            // for shape recognizability at 14–16px; tooltip carries the
            // literal system name so users never depend on glyph reading.
            switch (key)
            {
                case "agua fria":
                    return new Palette("#DBEAFE", "#1D4ED8", ""); // Drop
                case "agua quente":
                    return new Palette("#FEE2E2", "#DC2626", ""); // Important / heat
                case "esgoto":
                    return new Palette("#E7E5E4", "#7C2D12", ""); // Down arrow
                case "pluvial":
                    return new Palette("#CFFAFE", "#0F766E", ""); // Cloud / rain
                case "caixas e ralos":
                    return new Palette("#E2E8F0", "#334155", ""); // Package
                case "reservatorio":
                    return new Palette("#E0E7FF", "#4338CA", ""); // Box
                case "sted":
                    return new Palette("#DCFCE7", "#15803D", ""); // Pipe-like
                case "piscina":
                    return new Palette("#E0F2FE", "#0369A1", ""); // Wave
                case "irrigacao":
                    return new Palette("#ECFCCB", "#4D7C0F", ""); // Plant / leaf
                case "poco":
                    return new Palette("#FEF3C7", "#B45309", ""); // Hole
                case "bombas":
                    return new Palette("#FFEDD5", "#C2410C", ""); // Speedometer
                case "valvula":
                    return new Palette("#F3E8FF", "#7E22CE", ""); // Settings/gear
                case "utilitario":
                    return new Palette("#F3F4F6", "#4B5563", ""); // Settings
                case "combate a incendio":
                    return new Palette("#FEE2E2", "#B91C1C", ""); // Fire
                default:
                    return new Palette(
                        "#EEF2FF",
                        "#4338CA",
                        FirstLetterGlyph(description));
            }
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
