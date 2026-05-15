using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace DarivaBIM.Infrastructure.Persistence.Preferences
{
    /// <summary>
    /// Persiste preferências locais do usuário para a janela de Importar
    /// Famílias: lista de favoritas e histórico de importações recentes.
    ///
    /// Armazenamento: <c>%LocalAppData%/DarivaBIM/family-prefs.json</c>.
    /// Local em vez de roaming porque a preferência é por máquina (uma
    /// família "favorita" reflete o trabalho local; sincronizar entre PCs
    /// causa surpresa). Falha de leitura/escrita degrada para in-memory
    /// silenciosamente — preferência não é dado crítico.
    /// </summary>
    public sealed class FamilyPreferencesService
    {
        // Cap do histórico: 50 entradas é mais do que o usuário típico
        // visualiza num scroll. Acima disso, a aba "Recentes" vira lista
        // longa e perde valor.
        private const int RecentLimit = 50;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
        };

        private readonly string _filePath;
        private readonly object _lock = new();
        private FamilyPreferencesData _data;

        public FamilyPreferencesService()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string folder = Path.Combine(appData, "DarivaBIM");
            Directory.CreateDirectory(folder);
            _filePath = Path.Combine(folder, "family-prefs.json");

            _data = LoadFromDisk();
        }

        public bool IsFavorite(int familyId)
        {
            lock (_lock)
            {
                return _data.Favorites.Contains(familyId);
            }
        }

        public IReadOnlyCollection<int> GetFavoriteIds()
        {
            lock (_lock)
            {
                return _data.Favorites.ToArray();
            }
        }

        /// <summary>Histórico de imports, mais recente primeiro.</summary>
        public IReadOnlyList<RecentFamilyEntry> GetRecents()
        {
            lock (_lock)
            {
                return _data.Recents.ToArray();
            }
        }

        /// <summary>
        /// Posicionamento persistido da janela de famílias (left/top/width/
        /// height + maximizado). Retorna <c>null</c> na primeira execução
        /// ou se o JSON estava corrompido — caller usa default próprio.
        /// </summary>
        public WindowPlacement? GetWindowPlacement()
        {
            lock (_lock)
            {
                return _data.WindowPlacement;
            }
        }

        public void SaveWindowPlacement(WindowPlacement placement)
        {
            lock (_lock)
            {
                _data.WindowPlacement = placement;
                SaveToDisk();
            }
        }

        /// <returns><c>true</c> se a família passou a ser favorita; <c>false</c> se foi desfavoritada.</returns>
        public bool ToggleFavorite(int familyId)
        {
            bool added;
            lock (_lock)
            {
                if (_data.Favorites.Contains(familyId))
                {
                    _data.Favorites.Remove(familyId);
                    added = false;
                }
                else
                {
                    _data.Favorites.Add(familyId);
                    added = true;
                }

                SaveToDisk();
            }
            return added;
        }

        /// <summary>
        /// Marca uma família como recém-importada. Se já estava no histórico,
        /// move para o topo (timestamp atualizado). Cap de RecentLimit
        /// entradas — entradas excedentes ao final são descartadas.
        /// </summary>
        public void RegisterRecentImport(int familyId)
        {
            lock (_lock)
            {
                _data.Recents.RemoveAll(r => r.FamilyId == familyId);
                _data.Recents.Insert(
                    0,
                    new RecentFamilyEntry { FamilyId = familyId, Timestamp = DateTime.UtcNow });

                if (_data.Recents.Count > RecentLimit)
                {
                    _data.Recents.RemoveRange(RecentLimit, _data.Recents.Count - RecentLimit);
                }

                SaveToDisk();
            }
        }

        private FamilyPreferencesData LoadFromDisk()
        {
            try
            {
                if (!File.Exists(_filePath))
                {
                    return new FamilyPreferencesData();
                }

                string json = File.ReadAllText(_filePath);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return new FamilyPreferencesData();
                }

                return JsonSerializer.Deserialize<FamilyPreferencesData>(json, JsonOptions)
                       ?? new FamilyPreferencesData();
            }
            catch
            {
                // JSON corrompido ou IO falha: começa do zero. Persistir
                // um corrompido degrada o valor; partir limpo dá ao usuário
                // a chance de re-favoritar conforme usa.
                return new FamilyPreferencesData();
            }
        }

        private void SaveToDisk()
        {
            try
            {
                string json = JsonSerializer.Serialize(_data, JsonOptions);
                File.WriteAllText(_filePath, json);
            }
            catch
            {
                // Falha de IO (disco cheio, permissão, antivírus interceptou):
                // mantém o estado em memória. Próxima alteração tenta de novo.
            }
        }
    }

    public sealed class FamilyPreferencesData
    {
        public HashSet<int> Favorites { get; set; } = new();
        public List<RecentFamilyEntry> Recents { get; set; } = new();
        public WindowPlacement? WindowPlacement { get; set; }
    }

    public sealed class RecentFamilyEntry
    {
        public int FamilyId { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public sealed class WindowPlacement
    {
        public double Left { get; set; }
        public double Top { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public bool IsMaximized { get; set; }
    }
}
