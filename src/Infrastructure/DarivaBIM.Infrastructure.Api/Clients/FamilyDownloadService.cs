using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
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

            client.DefaultRequestHeaders.Add("User-Agent", "FamiliesImporterHub");
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

                    // netstandard2.0 doesn't expose a CancellationToken overload
                    // of ReadAsStreamAsync; the GetAsync above already honours
                    // the token while reading headers, and CopyToAsync below
                    // covers cancellation during the body transfer.
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
                        await inputStream
                            .CopyToAsync(outputStream, bufferSize: 81920, cancellationToken)
                            .ConfigureAwait(false);

                        await outputStream
                            .FlushAsync(cancellationToken)
                            .ConfigureAwait(false);
                    }
                }

                if (File.Exists(cachedFilePath))
                {
                    File.Delete(cachedFilePath);
                }

                File.Move(tempFilePath, cachedFilePath);

                return cachedFilePath;
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
