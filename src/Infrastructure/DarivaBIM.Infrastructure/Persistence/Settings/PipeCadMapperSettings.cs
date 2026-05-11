using System;
using System.IO;
using System.Text.Json;

namespace DarivaBIM.Infrastructure.Persistence.Settings
{
    /// <summary>
    /// Estado serializável do PipeCADMapper, persistido em
    /// <c>%APPDATA%\EVT-BIM\pipecadmapper.json</c>. Como os <c>ElementId</c>s
    /// mudam entre projetos, gravamos as seleções por NOME e reconstruímos a
    /// referência ao recarregar os dados do documento ativo. Caso o arquivo
    /// não exista no caminho atual mas exista no caminho legado
    /// <c>%APPDATA%\TigreBIM\pipecadmapper.json</c>, ele é carregado uma vez
    /// e regravado no novo caminho na próxima chamada a <see cref="Save"/>.
    /// </summary>
    public class PipeCadMapperSettings
    {
        public string? SystemName { get; set; }
        public string? PipeTypeName { get; set; }
        public double? DiameterMm { get; set; }
        public string? LevelName { get; set; }
        public double OffsetMm { get; set; }

        // Novos campos (workflow vínculo CAD → marcadores → tubos).
        // LayerName é guardado entre sessões para que projetos com o mesmo
        // padrão de nomenclatura abram já apontando para o layer correto.
        public string? LayerName { get; set; }

        // "Unifilar" (default) ou "Bifilar". Mantemos como string para
        // tolerar evolução do enum sem quebrar settings existentes.
        public string? Mode { get; set; }

        // Quando true, os marcadores são criados na cota Z dos pontos do CAD
        // (ignorando o nível de referência selecionado para fins de elevação).
        // O nível selecionado continua sendo o host do placeholder.
        public bool UseCadElevation { get; set; }

        // Slider 0..100 que controla, em conjunto, os limiares do detector
        // bifilar. Padrão 50 representa o ponto médio "neutro".
        public double TolerancePercent { get; set; } = 50.0;

        private static string SettingsPath
        {
            get
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string dir = Path.Combine(appData, "EVT-BIM");
                return Path.Combine(dir, "pipecadmapper.json");
            }
        }

        private static string LegacySettingsPath
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
                string? sourcePath = null;

                if (File.Exists(path))
                {
                    sourcePath = path;
                }
                else
                {
                    string legacyPath = LegacySettingsPath;
                    if (File.Exists(legacyPath))
                    {
                        sourcePath = legacyPath;
                    }
                }

                if (sourcePath == null)
                    return new PipeCadMapperSettings();

                string json = File.ReadAllText(sourcePath);
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
