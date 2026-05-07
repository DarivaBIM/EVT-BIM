using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using DarivaBIM.Application.Common;
using DarivaBIM.Application.DTOs.Family;
using DarivaBIM.Infrastructure.Persistence.Cache;

namespace DarivaBIM.Infrastructure.Api.Clients
{
    public class FamilyDownloadService
    {
        private static readonly HttpClient HttpClient = CreateHttpClient();

        private static HttpClient CreateHttpClient()
        {
            var client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(60)
            };

            client.DefaultRequestHeaders.Add("User-Agent", FeatureNames.FamiliesImporter);
            return client;
        }

        /// <summary>
        /// Downloads the family file to the local cache and returns the cache path.
        /// Always call this BEFORE raising the Revit ExternalEvent — the
        /// ExternalEvent runs on Revit's UI thread and any I/O there freezes
        /// the entire Revit window until completion (see ADR-0015 follow-up).
        /// </summary>
        public async Task<string> DownloadToCacheAsync(
            ImportFamilyRequest request,
            FamilyCacheService cacheService,
            IProgress<DownloadProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            if (cacheService == null)
                throw new ArgumentNullException(nameof(cacheService));

            if (!request.HasDownloadUrl)
                throw new InvalidOperationException("A família não possui URL de download.");

            string cachedFilePath = cacheService.GetCachedFilePath(request);
            string legacyFilePath = cacheService.GetLegacyFilePathWithoutExtension(request);

            HealLegacyFileIfNeeded(legacyFilePath, cachedFilePath);

            if (File.Exists(cachedFilePath))
            {
                FileInfo fileInfo = new FileInfo(cachedFilePath);

                if (fileInfo.Length > 0)
                {
                    progress?.Report(new DownloadProgress(fileInfo.Length, fileInfo.Length));
                    return cachedFilePath;
                }
            }

            string tempFilePath = cachedFilePath + ".download";

            try
            {
                if (File.Exists(tempFilePath))
                {
                    File.Delete(tempFilePath);
                }

                using (HttpResponseMessage response = await HttpClient
                           .GetAsync(
                               request.DownloadUrl,
                               HttpCompletionOption.ResponseHeadersRead,
                               cancellationToken)
                           .ConfigureAwait(false))
                {
                    response.EnsureSuccessStatusCode();

                    long? totalBytes = response.Content.Headers.ContentLength;
                    progress?.Report(new DownloadProgress(0, totalBytes));

                    // Manual read loop instead of CopyToAsync so we can emit
                    // progress notifications. Buffer size matches what the
                    // previous CopyToAsync used so behavior is unchanged for
                    // the no-progress case.
                    using (Stream inputStream = await response.Content
                               .ReadAsStreamAsync()
                               .ConfigureAwait(false))
                    using (FileStream outputStream = new FileStream(
                               tempFilePath,
                               FileMode.Create,
                               FileAccess.Write,
                               FileShare.None,
                               bufferSize: 81920,
                               useAsync: true))
                    {
                        byte[] buffer = new byte[81920];
                        long totalRead = 0;
                        int read;

                        while ((read = await inputStream
                                   .ReadAsync(buffer, 0, buffer.Length, cancellationToken)
                                   .ConfigureAwait(false)) > 0)
                        {
                            await outputStream
                                .WriteAsync(buffer, 0, read, cancellationToken)
                                .ConfigureAwait(false);

                            totalRead += read;
                            progress?.Report(new DownloadProgress(totalRead, totalBytes));
                        }

                        await outputStream
                            .FlushAsync(cancellationToken)
                            .ConfigureAwait(false);
                    }
                }

                return await ReplaceCachedFileAsync(tempFilePath, cachedFilePath, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch
            {
                if (File.Exists(tempFilePath))
                {
                    try
                    {
                        File.Delete(tempFilePath);
                    }
                    catch
                    {
                    }
                }

                throw;
            }
        }

        // Atomically swap the freshly-downloaded temp file into the canonical
        // cache path. The cache file may be transiently locked by antivirus
        // post-write scanning or by Revit holding the previously-loaded .rfa
        // open in the current session — both surface as IOException. We retry
        // a handful of times, then fall back to a sibling unique path so the
        // import can proceed instead of the user having to close the panel
        // and try again.
        private static async Task<string> ReplaceCachedFileAsync(
            string tempFilePath,
            string cachedFilePath,
            CancellationToken cancellationToken)
        {
            const int maxAttempts = 5;

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                try
                {
                    if (File.Exists(cachedFilePath))
                    {
                        File.Delete(cachedFilePath);
                    }

                    File.Move(tempFilePath, cachedFilePath);
                    return cachedFilePath;
                }
                catch (Exception ex) when (
                    (ex is IOException || ex is UnauthorizedAccessException)
                    && attempt < maxAttempts - 1)
                {
                    int delayMs = 120 * (attempt + 1);
                    await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
                }
            }

            string fallbackPath = Path.Combine(
                Path.GetDirectoryName(cachedFilePath)!,
                Path.GetFileNameWithoutExtension(cachedFilePath)
                    + "_" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff")
                    + Path.GetExtension(cachedFilePath));

            File.Move(tempFilePath, fallbackPath);
            return fallbackPath;
        }

        private static void HealLegacyFileIfNeeded(string legacyPathWithoutExtension, string correctRfaPath)
        {
            if (!File.Exists(legacyPathWithoutExtension))
            {
                return;
            }

            if (File.Exists(correctRfaPath))
            {
                return;
            }

            File.Move(legacyPathWithoutExtension, correctRfaPath);
        }
    }
}
