using System;
using System.IO;
using System.Text.Json;
using DarivaBIM.Application.Contracts.UtilizationPoints;
using DarivaBIM.Application.DTOs.UtilizationPoints;

namespace DarivaBIM.Infrastructure.Persistence.UtilizationPoints
{
    /// <summary>
    /// Persiste os grupos de pontos de utilização em
    /// <c>%AppData%\DarivaBIM\EVT-BIM\UtilizationPoints\profiles.json</c>.
    /// Falhas de IO/JSON são silenciosas no <see cref="Load"/> (devolve um
    /// envelope vazio) e no <see cref="Save"/> (best-effort) para não derrubar
    /// a janela WPF.
    /// </summary>
    public sealed class UtilizationPointSettingsStore : IUtilizationPointSettingsStore
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
        };

        private static string ProfilesPath
        {
            get
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string dir = Path.Combine(appData, "DarivaBIM", "EVT-BIM", "UtilizationPoints");
                return Path.Combine(dir, "profiles.json");
            }
        }

        public UtilizationPointProfilesDto Load()
        {
            try
            {
                string path = ProfilesPath;
                if (!File.Exists(path))
                    return new UtilizationPointProfilesDto();

                string json = File.ReadAllText(path);
                UtilizationPointProfilesDto? loaded = JsonSerializer.Deserialize<UtilizationPointProfilesDto>(json);
                return loaded ?? new UtilizationPointProfilesDto();
            }
            catch
            {
                return new UtilizationPointProfilesDto();
            }
        }

        public void Save(UtilizationPointProfilesDto profiles)
        {
            if (profiles == null) return;

            try
            {
                string path = ProfilesPath;
                string? dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                string json = JsonSerializer.Serialize(profiles, JsonOptions);

                File.WriteAllText(path, json);
            }
            catch
            {
                // Persistência best-effort.
            }
        }
    }
}
