using System;
using System.IO;
using System.Net.Http;
using FamiliesImporterHub.Infrastructure;

namespace FamiliesImporterHub.Services
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

        public string DownloadToCache(ImportFamilyRequest request, FamilyCacheService cacheService)
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

                using (HttpResponseMessage response = HttpClient
                           .GetAsync(request.DownloadUrl, HttpCompletionOption.ResponseHeadersRead)
                           .GetAwaiter()
                           .GetResult())
                {
                    response.EnsureSuccessStatusCode();

                    using (Stream inputStream = response.Content
                               .ReadAsStreamAsync()
                               .GetAwaiter()
                               .GetResult())
                    using (FileStream outputStream = new FileStream(
                               tempFilePath,
                               FileMode.Create,
                               FileAccess.Write,
                               FileShare.None))
                    {
                        inputStream.CopyTo(outputStream);
                        outputStream.Flush();
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