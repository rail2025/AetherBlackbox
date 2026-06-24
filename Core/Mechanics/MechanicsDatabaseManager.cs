using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace AetherBlackbox.Core.Mechanics
{
    public static class MechanicDatabaseManager
    {
        private static string GetFilePath(uint territoryId)
        {
            var configDir = Service.PluginInterface.ConfigDirectory.FullName;
            var mechanicsDir = Path.Combine(configDir, "Mechanics");

            if (!Directory.Exists(mechanicsDir))
            {
                Directory.CreateDirectory(mechanicsDir);
            }

            return Path.Combine(mechanicsDir, $"{territoryId}.json");
        }

        public static List<CustomMechanicEntry> LoadTerritory(uint territoryId)
        {
            var path = GetFilePath(territoryId);

            if (!File.Exists(path))
            {
                return new List<CustomMechanicEntry>();
            }

            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<List<CustomMechanicEntry>>(json) ?? new List<CustomMechanicEntry>();
        }

        public static void SaveTerritory(uint territoryId, List<CustomMechanicEntry> entries)
        {
            var path = GetFilePath(territoryId);
            var json = JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
        }
    }
}