using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace DarivaBIM.Application.DTOs.Family
{
    public class FamilyItem
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string FileName { get; set; } = string.Empty;

        public bool IsPremium { get; set; }

        public List<string> Keywords { get; set; } = new();

        public int ManufacturerId { get; set; }

        public ManufacturerItem? Manufacturer { get; set; }

        public List<FamilyTag> Tags { get; set; } = new();

        public string UrlImg { get; set; } = string.Empty;

        public string UrlCatalog { get; set; } = string.Empty;

        public string? Youtube { get; set; }

        public List<DownloadLink> DownloadLinks { get; set; } = new();

        public DateTime? CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public string SearchIndex { get; set; } = string.Empty;

        public string SearchIndexCompact { get; set; } = string.Empty;

        public string ManufacturerName
            => Manufacturer?.Name ?? "Fabricante não informado";

        public string CreatedAtLabel
            => CreatedAt.HasValue
                ? CreatedAt.Value.ToString("dd/MM/yy")
                : "Não informado";

        public string UpdatedAtLabel
            => UpdatedAt.HasValue
                ? UpdatedAt.Value.ToString("dd/MM/yy")
                : "Não informado";

        public bool HasCatalogUrl
            => !string.IsNullOrWhiteSpace(UrlCatalog);

        public bool HasYoutubeUrl
            => !string.IsNullOrWhiteSpace(Youtube);

        public bool HasImageUrl
            => !string.IsNullOrWhiteSpace(UrlImg);

        public bool HasTags
            => Tags != null && Tags.Any(tag => !string.IsNullOrWhiteSpace(tag.Description));

        public List<string> DisplayTags
            => Tags == null
                ? new List<string>()
                : Tags
                    .Where(tag => !string.IsNullOrWhiteSpace(tag.Description))
                    .Select(tag => tag.Description.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

        public List<TagBadge> DisplayTagBadges
        {
            get
            {
                List<string> tags = DisplayTags;

                if (tags.Count <= 2)
                {
                    return tags
                        .Select(CreateTagBadge)
                        .ToList();
                }

                List<TagBadge> visibleBadges = tags
                    .Take(2)
                    .Select(CreateTagBadge)
                    .ToList();

                List<string> hiddenTags = tags
                    .Skip(2)
                    .ToList();

                visibleBadges.Add(
                    new TagBadge(
                        $"+{hiddenTags.Count}",
                        "#F1F5F9",
                        "#475569",
                        "Outras tags:\n" + string.Join("\n", hiddenTags)));

                return visibleBadges;
            }
        }

        public int LatestVersion
            => DownloadLinks == null || DownloadLinks.Count == 0
                ? 0
                : DownloadLinks.Max(link => link.Version);

        public string BestDownloadUrl
            => DownloadLinks == null || DownloadLinks.Count == 0
                ? string.Empty
                : DownloadLinks
                    .OrderByDescending(link => link.Version)
                    .First()
                    .UrlDownload;

        public override string ToString()
        {
            return Name;
        }

        private static TagBadge CreateTagBadge(string tag)
        {
            string key = NormalizeKey(tag);

            switch (key)
            {
                case "agua fria":
                    return new TagBadge(tag, "#DBEAFE", "#1D4ED8");

                case "agua quente":
                    return new TagBadge(tag, "#FEE2E2", "#DC2626");

                case "esgoto":
                    return new TagBadge(tag, "#E7E5E4", "#7C2D12");

                case "pluvial":
                    return new TagBadge(tag, "#CFFAFE", "#0F766E");

                case "caixas e ralos":
                    return new TagBadge(tag, "#E2E8F0", "#334155");

                case "reservatorio":
                    return new TagBadge(tag, "#E0E7FF", "#4338CA");

                case "sted":
                    return new TagBadge(tag, "#DCFCE7", "#15803D");

                case "piscina":
                    return new TagBadge(tag, "#E0F2FE", "#0369A1");

                case "irrigacao":
                    return new TagBadge(tag, "#ECFCCB", "#4D7C0F");

                case "poco":
                    return new TagBadge(tag, "#FEF3C7", "#B45309");

                case "bombas":
                    return new TagBadge(tag, "#FFEDD5", "#C2410C");

                case "valvula":
                    return new TagBadge(tag, "#F3E8FF", "#7E22CE");

                case "utilitario":
                    return new TagBadge(tag, "#F3F4F6", "#4B5563");

                case "combate a incendio":
                    return new TagBadge(tag, "#FEE2E2", "#B91C1C");

                default:
                    return new TagBadge(tag, "#EEF2FF", "#4338CA");
            }
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

    public class TagBadge
    {
        public TagBadge(string text, string background, string foreground, string? toolTipText = null)
        {
            Text = text;
            Background = background;
            Foreground = foreground;
            ToolTipText = toolTipText;
        }

        public string Text { get; }

        public string Background { get; }

        public string Foreground { get; }

        public string? ToolTipText { get; }
    }
}