using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DarivaBIM.Application.Common;

namespace DarivaBIM.Infrastructure.Persistence.Cache
{
    /// <summary>
    /// On-disk cache for thumbnail images. Owns the cache folder, the
    /// URL-to-path hashing, and atomic writes through a sibling temp file.
    /// HTTP fetching lives in <c>Infrastructure.Api.Clients.ThumbnailDownloader</c>
    /// — Persistence/ must not depend on System.Net.Http (see ADR-0016 and
    /// the InfrastructureBoundariesTests).
    /// </summary>
    public class ThumbnailCacheService
    {
        private readonly string _cacheFolder;

        public ThumbnailCacheService()
        {
            _cacheFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                FeatureNames.FamiliesImporter,
                "ThumbnailCache");

            Directory.CreateDirectory(_cacheFolder);
        }

        public string CacheFolder => _cacheFolder;

        /// <summary>
        /// Returns the local cache path if the thumbnail is already on disk,
        /// or <c>null</c> otherwise. Lets callers take a synchronous fast path
        /// when virtualized containers recycle into a thumbnail we already have.
        /// </summary>
        public string? TryGetCachedPath(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return null;
            }

            string cachePath = ComputeCachePath(url);
            return File.Exists(cachePath) ? cachePath : null;
        }

        /// <summary>
        /// Canonical on-disk path for a given URL. Stable across sessions so
        /// the downloader can decide whether to fetch and where to write.
        /// </summary>
        public string ComputeCachePath(string url)
        {
            return Path.Combine(_cacheFolder, ComputeStableFileName(url));
        }

        /// <summary>
        /// Persists <paramref name="source"/> at the canonical cache path for
        /// <paramref name="url"/>, writing first to a sibling <c>.download</c>
        /// temp file and atomically moving it into place. Returns the final
        /// path on success, or <c>null</c> if the write failed.
        /// </summary>
        public async Task<string?> StoreFromStreamAsync(
            string url,
            Stream source,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(url) || source == null)
            {
                return null;
            }

            string cachePath = ComputeCachePath(url);
            string tempPath = cachePath + ".download";

            try
            {
                using (FileStream outputStream = new FileStream(
                    tempPath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 81920,
                    useAsync: true))
                {
                    await source.CopyToAsync(outputStream, 81920, cancellationToken).ConfigureAwait(false);
                    await outputStream.FlushAsync(cancellationToken).ConfigureAwait(false);
                }

                if (File.Exists(cachePath))
                {
                    File.Delete(cachePath);
                }

                File.Move(tempPath, cachePath);
                return cachePath;
            }
            catch
            {
                try
                {
                    if (File.Exists(tempPath))
                    {
                        File.Delete(tempPath);
                    }
                }
                catch
                {
                }

                return null;
            }
        }

        private static string ComputeStableFileName(string url)
        {
            byte[] hash;

            using (SHA1 sha = SHA1.Create())
            {
                hash = sha.ComputeHash(Encoding.UTF8.GetBytes(url));
            }

            string hashHex = ToHexLowerInvariant(hash);
            string extension = ExtractWhitelistedExtension(url);

            return string.IsNullOrEmpty(extension) ? hashHex : hashHex + extension;
        }

        private static string ExtractWhitelistedExtension(string url)
        {
            try
            {
                Uri uri = new Uri(url);
                string ext = Path.GetExtension(uri.AbsolutePath);

                if (string.IsNullOrEmpty(ext) || ext.Length > 5)
                {
                    return string.Empty;
                }

                string lower = ext.ToLowerInvariant();

                return lower is ".png" or ".jpg" or ".jpeg" or ".gif" or ".webp" or ".bmp"
                    ? lower
                    : string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string ToHexLowerInvariant(byte[] bytes)
        {
            const string HexAlphabet = "0123456789abcdef";

            char[] result = new char[bytes.Length * 2];

            for (int i = 0; i < bytes.Length; i++)
            {
                result[i * 2] = HexAlphabet[bytes[i] >> 4];
                result[i * 2 + 1] = HexAlphabet[bytes[i] & 0xF];
            }

            return new string(result);
        }
    }
}
