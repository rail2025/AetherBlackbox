using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Text.Json;

namespace AetherBlackbox.Core.Mechanics
{
    public class PresetStorageService
    {
        private readonly string storageDirectory;

        public PresetStorageService(string basePath)
        {
            storageDirectory = Path.Combine(basePath, "MechanicPresets");
            if (!Directory.Exists(storageDirectory))
            {
                Directory.CreateDirectory(storageDirectory);
            }
        }

        public void Save(string filename, List<CustomMechanicEntry> entries)
        {
            string path = Path.Combine(storageDirectory, $"{filename}.json");
            string json = JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
        }

        public List<CustomMechanicEntry> Load(string filename)
        {
            string path = Path.Combine(storageDirectory, $"{filename}.json");
            if (!File.Exists(path)) return new List<CustomMechanicEntry>();

            string json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<List<CustomMechanicEntry>>(json) ?? new List<CustomMechanicEntry>();
        }

        public void Delete(string filename)
        {
            string path = Path.Combine(storageDirectory, $"{filename}.json");
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}