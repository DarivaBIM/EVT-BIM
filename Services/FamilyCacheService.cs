using System;
using System.IO;
using FamiliesImporterHub.Infrastructure;

namespace FamiliesImporterHub.Services
{
    public class FamilyCacheService
    {
        private readonly string _rootCacheFolder;

        public FamilyCacheService()
        {
            _rootCacheFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "FamiliesImporterHub",
                "Cache");
        }

        public string RootCacheFolder => _rootCacheFolder;

        public string GetFamilyVersionFolder(ImportFamilyRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            string manufacturerFolder = SanitizePathSegment(
                string.IsNullOrWhiteSpace(request.ManufacturerName)
                    ? "SemFabricante"
                    : request.ManufacturerName);

            string familyFolder = $"{request.FamilyId:D6}_{SanitizePathSegment(request.FamilyName)}";
            string versionFolder = $"v{Math.Max(0, request.LatestVersion)}";

            return Path.Combine(_rootCacheFolder, manufacturerFolder, familyFolder, versionFolder);
        }

        public string GetCachedFilePath(ImportFamilyRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            string versionFolder = GetFamilyVersionFolder(request);
            Directory.CreateDirectory(versionFolder);

            string ensuredFileName = EnsureRfaFileName(request.ResolvedFileName);

            return Path.Combine(versionFolder, ensuredFileName);
        }

        public string GetLegacyFilePathWithoutExtension(ImportFamilyRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            string versionFolder = GetFamilyVersionFolder(request);
            Directory.CreateDirectory(versionFolder);

            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(request.ResolvedFileName);

            return Path.Combine(versionFolder, fileNameWithoutExtension);
        }

        private static string EnsureRfaFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return "familia.rfa";
            }

            string safeName = Path.GetFileName(fileName);

            if (!safeName.EndsWith(".rfa", StringComparison.OrdinalIgnoreCase))
            {
                safeName = Path.GetFileNameWithoutExtension(safeName) + ".rfa";
            }

            return safeName;
        }

        private static string SanitizePathSegment(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "SemNome";

            char[] invalidChars = Path.GetInvalidFileNameChars();
            char[] chars = value.Trim().ToCharArray();

            for (int i = 0; i < chars.Length; i++)
            {
                if (Array.IndexOf(invalidChars, chars[i]) >= 0)
                {
                    chars[i] = '_';
                }
            }

            string sanitized = new string(chars);

            while (sanitized.Contains("  "))
            {
                sanitized = sanitized.Replace("  ", " ");
            }

            sanitized = sanitized.Trim();

            return string.IsNullOrWhiteSpace(sanitized)
                ? "SemNome"
                : sanitized;
        }
    }
}