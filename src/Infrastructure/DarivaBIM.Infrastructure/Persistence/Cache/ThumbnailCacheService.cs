using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DarivaBIM.Infrastructure.Persistence.Cache
{
    /// <summary>
    /// On-disk cache for thumbnail images fetched from remote URLs. The cache
    /// key is a SHA1 of the URL, so the same image referenced from anywhere
    /// in the app resolves to a single file. Concurrent requests for the same
    /// URL are deduplicated through an in-flight task table to avoid wasting
    /// bandwidth when many cards are realized at once during scroll.
    /// </summary>
    public class ThumbnailCacheService
    {
        private static readonly HttpClient HttpClient = CreateHttpClient();

        private readonly string _cacheFolder;
        private readonly ConcurrentDictionary<string, Task<string?>> _inFlight = new();

        public ThumbnailCacheService()
        {
            _cacheFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "FamiliesImporterHub",
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

        public Task<string?> GetOrDownloadAsync(string url, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return Task.FromResult<string?>(null);
            }

            string cachePath = ComputeCachePath(url);

            if (File.Exists(cachePath))
            {
                return Task.FromResult<string?>(cachePath);
            }

            return _inFlight.GetOrAdd(url, capturedUrl => DownloadAsync(capturedUrl, cachePath, cancellationToken));
        }

        private async Task<string?> DownloadAsync(string url, string cachePath, CancellationToken cancellationToken)
        {
            string tempPath = cachePath + ".download";

            try
            {
                using (HttpResponseMessage response = await HttpClient
                    .GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                    .ConfigureAwait(false))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        return null;
                    }

                    using (Stream inputStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                    using (FileStream outputStream = new FileStream(
                        tempPath,
                        FileMode.Create,
                        FileAccess.Write,
                        FileShare.None,
                        bufferSize: 81920,
                        useAsync: true))
                    {
                        await inputStream.CopyToAsync(outputStream, 81920, cancellationToken).ConfigureAwait(false);
                        await outputStream.FlushAsync(cancellationToken).ConfigureAwait(false);
                    }
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
            finally
            {
                _inFlight.TryRemove(url, out _);
            }
        }

        private string ComputeCachePath(string url)
        {
            return Path.Combine(_cacheFolder, ComputeStableFileName(url));
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

        private static HttpClient CreateHttpClient()
        {
            var client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(30)
            };

            client.DefaultRequestHeaders.Add("User-Agent", "FamiliesImporterHub");
            return client;
        }
    }
}
