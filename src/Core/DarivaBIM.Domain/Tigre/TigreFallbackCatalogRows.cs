using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;

namespace DarivaBIM.Domain.Tigre
{
    /// <summary>
    /// Embedded fallback catalog used when the JSON file on disk cannot be
    /// loaded. Reads the catalog from the assembly's embedded resource
    /// <c>DarivaBIM.Domain.Tigre.tigre_codes.json</c>, which is the same
    /// file the plugins copy to <c>Resources/tigre_codes.json</c> at build
    /// time — single source of truth for the catalog.
    /// </summary>
    public static class TigreFallbackCatalogRows
    {
        private const string EmbeddedResourceName = "DarivaBIM.Domain.Tigre.tigre_codes.json";

        public static IEnumerable<TigreRawCatalogRow> All()
        {
            Assembly assembly = typeof(TigreFallbackCatalogRows).Assembly;
            using Stream? stream = assembly.GetManifestResourceStream(EmbeddedResourceName);

            if (stream == null)
            {
                throw new InvalidOperationException(
                    $"Embedded resource '{EmbeddedResourceName}' not found in {assembly.FullName}.");
            }

            List<TigreRawCatalogRow>? rows = JsonSerializer.Deserialize<List<TigreRawCatalogRow>>(
                stream,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return rows ?? new List<TigreRawCatalogRow>();
        }
    }
}
