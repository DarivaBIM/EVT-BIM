using System;
using System.IO;
using System.Text.Json;

namespace FamiliesImporterHub.Infrastructure
{
    /// <summary>
    /// Estado serializável do PipeCADMapper, persistido em
    /// <c>%APPDATA%\TigreBIM\pipecadmapper.json</c>. Como os <c>ElementId</c>s
    /// mudam entre projetos, gravamos as seleções por NOME e reconstruímos a
    /// referência ao recarregar os dados do documento ativo.
    /// </summary>
    public class PipeCadMapperSettings
    {
        public string? SystemName { get; set; }
        public string? PipeTypeName { get; set; }
        public double? DiameterMm { get; set; }
        public string? LevelName { get; set; }
        public double OffsetMm { get; set; }

        private static string SettingsPath
        {
            get
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string dir = Path.Combine(appData, "TigreBIM");
                return Path.Combine(dir, "pipecadmapper.json");
            }
        }

        public static PipeCadMapperSettings Load()
        {
            try
            {
                string path = SettingsPath;
                if (!File.Exists(path))
                    return new PipeCadMapperSettings();

                string json = File.ReadAllText(path);
                PipeCadMapperSettings? loaded = JsonSerializer.Deserialize<PipeCadMapperSettings>(json);
                return loaded ?? new PipeCadMapperSettings();
            }
            catch
            {
                return new PipeCadMapperSettings();
            }
        }

        public void Save()
        {
            try
            {
                string path = SettingsPath;
                string? dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                string json = JsonSerializer.Serialize(this, new JsonSerializerOptions
                {
                    WriteIndented = true,
                });

                File.WriteAllText(path, json);
            }
            catch
            {
                // Persistência best-effort — falha silenciosa.
            }
        }
    }
}
