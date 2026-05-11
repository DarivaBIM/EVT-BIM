using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace DarivaBIM.Infrastructure.Persistence.Settings
{
    /// <summary>
    /// Estado serializável da ferramenta "Adicionar Prolongadores",
    /// persistido em <c>%APPDATA%\EVT-BIM\floor-drain-extension.json</c>.
    /// Como ElementIds mudam entre projetos, gravamos as preferências por
    /// chave (FamilyName|SymbolName) e por NOME do PipeType escolhido —
    /// reconstruímos a referência ao recarregar os dados do documento ativo.
    /// </summary>
    public class FloorDrainExtensionSettings
    {
        public double LengthMeters { get; set; } = 0.5;

        public List<FloorDrainExtensionTypePreference> TypePreferences { get; set; } = new();

        private static string SettingsPath
        {
            get
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string dir = Path.Combine(appData, "EVT-BIM");
                return Path.Combine(dir, "floor-drain-extension.json");
            }
        }

        public static FloorDrainExtensionSettings Load()
        {
            try
            {
                string path = SettingsPath;
                if (!File.Exists(path))
                    return new FloorDrainExtensionSettings();

                string json = File.ReadAllText(path);
                FloorDrainExtensionSettings? loaded = JsonSerializer.Deserialize<FloorDrainExtensionSettings>(json);
                return loaded ?? new FloorDrainExtensionSettings();
            }
            catch
            {
                return new FloorDrainExtensionSettings();
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

        public string? GetPipeTypeName(string familyName, string symbolName)
        {
            string key = BuildKey(familyName, symbolName);
            foreach (FloorDrainExtensionTypePreference p in TypePreferences)
            {
                if (string.Equals(p.Key, key, StringComparison.Ordinal))
                    return p.PipeTypeName;
            }
            return null;
        }

        public void SetPipeTypeName(string familyName, string symbolName, string? pipeTypeName)
        {
            string key = BuildKey(familyName, symbolName);
            for (int i = 0; i < TypePreferences.Count; i++)
            {
                if (string.Equals(TypePreferences[i].Key, key, StringComparison.Ordinal))
                {
                    TypePreferences[i].PipeTypeName = pipeTypeName;
                    return;
                }
            }

            TypePreferences.Add(new FloorDrainExtensionTypePreference
            {
                Key = key,
                FamilyName = familyName,
                SymbolName = symbolName,
                PipeTypeName = pipeTypeName,
            });
        }

        public static string BuildKey(string familyName, string symbolName)
        {
            return $"{familyName ?? string.Empty}|{symbolName ?? string.Empty}";
        }
    }

    /// <summary>
    /// Preferência de PipeType para um tipo específico de caixa sifonada/seca,
    /// identificado pela combinação Família + Tipo (Symbol). Persistido junto
    /// com a lista em <see cref="FloorDrainExtensionSettings"/>.
    /// </summary>
    public class FloorDrainExtensionTypePreference
    {
        public string Key { get; set; } = string.Empty;
        public string FamilyName { get; set; } = string.Empty;
        public string SymbolName { get; set; } = string.Empty;
        public string? PipeTypeName { get; set; }
    }
}
