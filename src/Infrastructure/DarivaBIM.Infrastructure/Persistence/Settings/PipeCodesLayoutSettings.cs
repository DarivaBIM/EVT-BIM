using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace DarivaBIM.Infrastructure.Persistence.Settings
{
    /// <summary>
    /// Snapshot persistente das larguras de coluna do PipeCodesWindow.
    /// Slice 4.3.B P2.7 — usuario pode redimensionar colunas via
    /// GridSplitter; widths sobrevivem entre sessoes em
    /// <c>%LocalAppData%\EVT-BIM\PipeCodesWindow.layout.json</c>.
    ///
    /// Persistencia best-effort: arquivo corrompido ou ausente devolve
    /// settings default (mesmas widths do XAML inicial). Sem schema
    /// version explicito - JSON simples + dicionario de chaves
    /// conhecidas (CheckBox, Qtd, Elemento, Diametro, CodIcon).
    /// </summary>
    public sealed class PipeCodesLayoutSettings
    {
        /// <summary>
        /// Larguras (em DIP, double) por nome de coluna. Chaves
        /// reconhecidas: CheckBox / Qtd / Elemento / Diametro / CodIcon.
        /// "Elemento" e a coluna estrela (Width="*" no XAML); o valor
        /// salvo NAO se aplica nela (ela continua * pra preencher o
        /// espaco). Salvamos so as colunas de width fixa.
        /// </summary>
        public Dictionary<string, double> Columns { get; set; }
            = new Dictionary<string, double>(StringComparer.Ordinal);
    }

    /// <summary>
    /// Store JSON em <c>%LocalAppData%\EVT-BIM\PipeCodesWindow.layout.json</c>.
    /// Sync simples (single user, single load) - sem async, sem mutex.
    /// Falhas de IO sao silenciosas e a janela cai pra defaults (widths
    /// do XAML).
    /// </summary>
    public sealed class PipeCodesLayoutStore
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
        };

        private static string SettingsPath
        {
            get
            {
                string localAppData = Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData);
                string dir = Path.Combine(localAppData, "EVT-BIM");
                return Path.Combine(dir, "PipeCodesWindow.layout.json");
            }
        }

        public PipeCodesLayoutSettings Load()
        {
            try
            {
                string path = SettingsPath;
                if (!File.Exists(path))
                    return new PipeCodesLayoutSettings();

                string json = File.ReadAllText(path);
                PipeCodesLayoutSettings? loaded =
                    JsonSerializer.Deserialize<PipeCodesLayoutSettings>(json);
                return loaded ?? new PipeCodesLayoutSettings();
            }
            catch
            {
                return new PipeCodesLayoutSettings();
            }
        }

        public void Save(PipeCodesLayoutSettings settings)
        {
            if (settings == null) return;

            try
            {
                string path = SettingsPath;
                string? dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                string json = JsonSerializer.Serialize(settings, JsonOptions);
                // Codex LOW#8 fix: atomic write via tmp + File.Replace.
                // File.WriteAllText trunca e grava direto — duas instancias
                // do Revit fechando Codificar Tigre quase juntas podiam
                // produzir JSON parcial/corrompido. Tmp + Replace garante
                // que o leitor sempre ve arquivo completo (Replace e atomic
                // em NTFS quando target existe).
                string tmpPath = path + ".tmp";
                File.WriteAllText(tmpPath, json);
                if (File.Exists(path))
                {
                    File.Replace(tmpPath, path, destinationBackupFileName: null);
                }
                else
                {
                    File.Move(tmpPath, path);
                }
            }
            catch
            {
                // Persistencia best-effort.
            }
        }
    }
}
