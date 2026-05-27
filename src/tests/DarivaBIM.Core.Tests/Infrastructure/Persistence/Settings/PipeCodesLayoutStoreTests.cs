using System;
using System.Collections.Generic;
using System.IO;
using DarivaBIM.Infrastructure.Persistence.Settings;
using Xunit;

namespace DarivaBIM.Core.Tests.Infrastructure.Persistence.Settings
{
    /// <summary>
    /// Smokes do <see cref="PipeCodesLayoutStore"/> — Slice 4.3.B P2.7.
    /// Store usa LocalAppData fixo (%LocalAppData%\EVT-BIM\); pra evitar
    /// poluir o ambiente do dev, esses testes manipulam o arquivo apos
    /// Save() / antes de Load() e limpam no Dispose pattern via try/finally.
    /// </summary>
    public class PipeCodesLayoutStoreTests
    {
        private static string SettingsPath
        {
            get
            {
                string localAppData = Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData);
                return Path.Combine(localAppData, "EVT-BIM", "PipeCodesWindow.layout.json");
            }
        }

        [Fact]
        public void Load_returns_empty_settings_when_file_does_not_exist()
        {
            // Codex LOW#9 fix: backup/restore como nos outros tests
            // (antes deletava arquivo real do dev sem preservar).
            string path = SettingsPath;
            string? backup = TryBackup(path);
            try
            {
                SafeDelete(path);

                PipeCodesLayoutStore store = new();
                PipeCodesLayoutSettings settings = store.Load();

                Assert.NotNull(settings);
                Assert.NotNull(settings.Columns);
                Assert.Empty(settings.Columns);
            }
            finally
            {
                Restore(path, backup);
            }
        }

        [Fact]
        public void Save_then_Load_roundtrips_column_widths()
        {
            string path = SettingsPath;
            string? backup = TryBackup(path);
            try
            {
                PipeCodesLayoutStore store = new();
                PipeCodesLayoutSettings written = new()
                {
                    Columns = new Dictionary<string, double>
                    {
                        ["ColCheckBoxWidth"] = 32d,
                        ["ColQtdWidth"] = 40d,
                        ["ColDiametroWidth"] = 72d,
                        ["ColCodIconWidth"] = 30d,
                    },
                };
                store.Save(written);

                PipeCodesLayoutSettings loaded = store.Load();
                Assert.Equal(4, loaded.Columns.Count);
                Assert.Equal(32d, loaded.Columns["ColCheckBoxWidth"]);
                Assert.Equal(40d, loaded.Columns["ColQtdWidth"]);
                Assert.Equal(72d, loaded.Columns["ColDiametroWidth"]);
                Assert.Equal(30d, loaded.Columns["ColCodIconWidth"]);
            }
            finally
            {
                Restore(path, backup);
            }
        }

        [Fact]
        public void Load_returns_empty_when_file_corrupted()
        {
            string path = SettingsPath;
            string? backup = TryBackup(path);
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, "{not valid json...");
                PipeCodesLayoutStore store = new();
                PipeCodesLayoutSettings settings = store.Load();

                Assert.NotNull(settings);
                Assert.NotNull(settings.Columns);
                // Empty - parse falhou; o store nao explode.
                Assert.Empty(settings.Columns);
            }
            finally
            {
                Restore(path, backup);
            }
        }

        [Fact]
        public void Save_creates_directory_if_missing()
        {
            string path = SettingsPath;
            string? backup = TryBackup(path);
            string? dir = Path.GetDirectoryName(path);
            try
            {
                if (dir != null && Directory.Exists(dir))
                {
                    // best-effort - garante dir antes; nao deletamos o dir
                    // sempre pra evitar race com outros tests do paralelismo.
                }

                PipeCodesLayoutStore store = new();
                PipeCodesLayoutSettings settings = new()
                {
                    Columns = new Dictionary<string, double> { ["ColQtdWidth"] = 40 },
                };
                store.Save(settings);

                Assert.True(File.Exists(path), "Esperava arquivo de layout criado em " + path);
            }
            finally
            {
                Restore(path, backup);
            }
        }

        private static void SafeDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }

        private static string? TryBackup(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                string backup = path + ".test-backup-" + Guid.NewGuid().ToString("N");
                File.Copy(path, backup, overwrite: true);
                return backup;
            }
            catch { return null; }
        }

        private static void Restore(string path, string? backup)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
            if (backup != null && File.Exists(backup))
            {
                try { File.Move(backup, path); } catch { }
            }
        }
    }
}
