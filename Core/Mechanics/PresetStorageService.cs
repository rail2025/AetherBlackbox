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
        public List<string> GetAvailableFiles()
        {
            if (!Directory.Exists(storageDirectory)) return new List<string>();
            var files = new List<string>();
            foreach (var file in Directory.GetFiles(storageDirectory, "*.json"))
            {
                files.Add(Path.GetFileNameWithoutExtension(file));
            }
            return files;
        }

        public void Save(string filename, List<CustomMechanicEntry> entries)
        {
            string path = Path.Combine(storageDirectory, $"{filename}.json");
            var options = new JsonSerializerOptions { WriteIndented = true, IncludeFields = true };
            string json = JsonSerializer.Serialize(entries, options);
            File.WriteAllText(path, json);
        }

        public List<CustomMechanicEntry> Load(string filename)
        {
            string path = Path.Combine(storageDirectory, $"{filename}.json");
            if (!File.Exists(path)) return new List<CustomMechanicEntry>();

            string json = File.ReadAllText(path);
            var options = new JsonSerializerOptions { IncludeFields = true };
            var entries = JsonSerializer.Deserialize<List<CustomMechanicEntry>>(json, options) ?? new List<CustomMechanicEntry>();

            foreach (var entry in entries)
            {
                entry.OriginFile = filename;
            }

            return entries;
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