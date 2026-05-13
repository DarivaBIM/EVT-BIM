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

        // Quando true, o nível de referência é fixado no nível associado ao
        // vínculo CAD selecionado (e o dropdown de nível fica bloqueado). O
        // offset continua editável e somado sobre esse nível. Quando false,
        // o usuário escolhe o nível manualmente.
        // Nome do campo preservado para compat com settings antigos no disco.
        public bool UseCadElevation { get; set; }

        // Nível discreto de tolerância do detector bifilar.
        // Armazenado como string para sobreviver à evolução do enum sem
        // quebrar settings antigos. Valores válidos:
        // "VeryLow" / "Low" / "Medium" / "High" / "VeryHigh".
        public string? ToleranceLevel { get; set; }

        // Restrição de ângulos de bend nas polylines geradoras de marcadores
        // (unifilar e bifilar). Quando AllowAnyBendAngle é true OU não há
        // ângulos marcados, o detector preserva a geometria original; caso
        // contrário, snappa cada bend para o nominal mais próximo dentro
        // de ±15°. Bends |bend|<15° viram retas (independente de checkbox)
        // exceto quando "qualquer ângulo" estiver marcado.
        // Nullable para detectar settings antigos (sem o campo) e aplicar
        // os defaults — preserva o comportamento de versões anteriores.
        public bool? AllowAnyBendAngle { get; set; }
        public bool? AllowBendAngle22_5 { get; set; }
        public bool? AllowBendAngle45 { get; set; }
        public bool? AllowBendAngle60 { get; set; }
        public bool? AllowBendAngle90 { get; set; }

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
