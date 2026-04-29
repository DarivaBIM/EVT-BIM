using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using DarivaBIM.Domain.Tigre;

namespace DarivaBIM.Infrastructure.Persistence.TigreCatalog
{
    /// <summary>
    /// Loads the Tigre catalog rows from a JSON file on disk.
    /// Falls back to <see cref="TigreFallbackCatalogRows"/> when the file is
    /// missing or malformed.
    /// </summary>
    public sealed class TigreCatalogJsonLoader : ITigreCatalogProvider
    {
        private readonly string _jsonPath;

        public TigreCatalogJsonLoader(string? jsonPath = null)
        {
            _jsonPath = jsonPath ?? Path.Combine(AppContext.BaseDirectory, "Resources", "tigre_codes.json");
        }

        public Domain.Tigre.TigreCatalog Load()
        {
            IEnumerable<TigreRawCatalogRow> rows = LoadRowsFromJson(_jsonPath) ?? TigreFallbackCatalogRows.All();
            return new Domain.Tigre.TigreCatalog(rows);
        }

        private static IEnumerable<TigreRawCatalogRow>? LoadRowsFromJson(string jsonPath)
        {
            try
            {
                if (!File.Exists(jsonPath))
                    return null;

                string json = File.ReadAllText(jsonPath, Encoding.UTF8);
                List<TigreRawCatalogRow>? rows = JsonSerializer.Deserialize<List<TigreRawCatalogRow>>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                return rows;
            }
            catch
            {
                return null;
            }
        }
    }
}
