using System;
using System.IO;
using System.Linq;
using FamiliesImporterHub.Models;

namespace FamiliesImporterHub.Infrastructure
{
    public class ImportFamilyRequest
    {
        public int FamilyId { get; init; }

        public string FamilyName { get; init; } = string.Empty;

        public string ManufacturerName { get; init; } = string.Empty;

        public string FileName { get; init; } = string.Empty;

        public string DownloadUrl { get; init; } = string.Empty;

        public int LatestVersion { get; init; }

        public bool IsPremium { get; init; }

        public string CatalogUrl { get; init; } = string.Empty;

        public string YoutubeUrl { get; init; } = string.Empty;

        public string ImageUrl { get; init; } = string.Empty;

        public DateTime RequestedAtUtc { get; init; } = DateTime.UtcNow;

        public bool HasDownloadUrl =>
            !string.IsNullOrWhiteSpace(DownloadUrl);

        public string DisplayName =>
            string.IsNullOrWhiteSpace(ManufacturerName)
                ? FamilyName
                : $"{ManufacturerName} - {FamilyName}";

        public string ResolvedFileName
        {
            get
            {
                string candidate = ExtractCandidateFileName();

                candidate = Path.GetFileNameWithoutExtension(candidate);

                if (string.IsNullOrWhiteSpace(candidate))
                {
                    candidate = FamilyName;
                }

                candidate = SanitizeBaseFileName(candidate);

                if (string.IsNullOrWhiteSpace(candidate))
                {
                    candidate = "familia";
                }

                return candidate + ".rfa";
            }
        }

        public static ImportFamilyRequest FromFamily(FamilyItem family)
        {
            if (family == null)
                throw new ArgumentNullException(nameof(family));

            string bestDownloadUrl = family.DownloadLinks == null
                ? string.Empty
                : family.DownloadLinks
                    .OrderByDescending(link => link.Version)
                    .Select(link => link.UrlDownload)
                    .FirstOrDefault() ?? string.Empty;

            return new ImportFamilyRequest
            {
                FamilyId = family.Id,
                FamilyName = family.Name,
                ManufacturerName = family.ManufacturerName,
                FileName = family.FileName,
                DownloadUrl = bestDownloadUrl,
                LatestVersion = family.LatestVersion,
                IsPremium = family.IsPremium,
                CatalogUrl = family.UrlCatalog,
                YoutubeUrl = family.Youtube ?? string.Empty,
                ImageUrl = family.UrlImg,
                RequestedAtUtc = DateTime.UtcNow
            };
        }

        public override string ToString()
        {
            return $"{DisplayName} | v{LatestVersion} | {ResolvedFileName}";
        }

        private string ExtractCandidateFileName()
        {
            if (!string.IsNullOrWhiteSpace(FileName))
            {
                return FileName.Trim();
            }

            if (!string.IsNullOrWhiteSpace(DownloadUrl))
            {
                try
                {
                    var uri = new Uri(DownloadUrl);
                    string lastSegment = uri.Segments.LastOrDefault() ?? string.Empty;

                    if (!string.IsNullOrWhiteSpace(lastSegment))
                    {
                        return Uri.UnescapeDataString(lastSegment).Trim('/');
                    }
                }
                catch
                {
                }
            }

            return FamilyName;
        }

        private static string SanitizeBaseFileName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "familia";

            char[] invalidChars = Path.GetInvalidFileNameChars();

            string sanitized = new string(
                value
                    .Trim()
                    .Select(ch => invalidChars.Contains(ch) ? '_' : ch)
                    .ToArray());

            while (sanitized.Contains("  "))
            {
                sanitized = sanitized.Replace("  ", " ");
            }

            sanitized = sanitized.Trim();

            return string.IsNullOrWhiteSpace(sanitized)
                ? "familia"
                : sanitized;
        }
    }
}