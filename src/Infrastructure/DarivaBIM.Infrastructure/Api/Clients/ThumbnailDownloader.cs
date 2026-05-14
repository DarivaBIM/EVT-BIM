using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using DarivaBIM.Infrastructure.Persistence.Cache;

namespace DarivaBIM.Infrastructure.Api.Clients
{
    /// <summary>
    /// Downloads thumbnail images and persists them through a
    /// <see cref="ThumbnailCacheService"/>. Concurrent requests for the same
    /// URL are deduplicated through an in-flight task table to avoid wasting
    /// bandwidth when many cards are realized at once during scroll.
    /// HTTP belongs to Api/ — Persistence/ stays free of System.Net.Http
    /// (ADR-0016).
    /// </summary>
    public class ThumbnailDownloader
    {
        private static readonly HttpClient HttpClient =
            DarivaBimHttpClientFactory.Create(TimeSpan.FromSeconds(30));

        private readonly ThumbnailCacheService _cache;
        private readonly ConcurrentDictionary<string, Task<string?>> _inFlight = new();

        public ThumbnailDownloader(ThumbnailCacheService cache)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        }

        public string? TryGetCachedPath(string url) => _cache.TryGetCachedPath(url);

        public Task<string?> GetOrDownloadAsync(string url, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return Task.FromResult<string?>(null);
            }

            string? cached = _cache.TryGetCachedPath(url);
            if (cached != null)
            {
                return Task.FromResult<string?>(cached);
            }

            return _inFlight.GetOrAdd(url, capturedUrl => DownloadAsync(capturedUrl, cancellationToken));
        }

        private async Task<string?> DownloadAsync(string url, CancellationToken cancellationToken)
        {
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
                    {
                        return await _cache
                            .StoreFromStreamAsync(url, inputStream, cancellationToken)
                            .ConfigureAwait(false);
                    }
                }
            }
            catch
            {
                return null;
            }
            finally
            {
                _inFlight.TryRemove(url, out _);
            }
        }

    }
}
