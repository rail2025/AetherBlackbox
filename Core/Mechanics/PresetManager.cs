using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AetherBlackbox.Core.Mechanics
{
    public class PresetManager
    {
        private readonly List<CustomMechanicEntry> activeMemory = new();
        public event Action? OnMemoryChanged;
        private uint _lastLoadedTerritory;  

        public IReadOnlyList<CustomMechanicEntry> ActiveMemory => activeMemory.AsReadOnly();

        public void AddEntry(CustomMechanicEntry entry)
        {
            activeMemory.Add(entry);
            OnMemoryChanged?.Invoke();
        }

        public void RemoveEntry(CustomMechanicEntry entry)
        {
            activeMemory.Remove(entry);
            OnMemoryChanged?.Invoke();
        }

        public void PurgeReferences(Predicate<CustomMechanicEntry> match)
        {
            if (activeMemory.RemoveAll(match) > 0)
            {
                OnMemoryChanged?.Invoke();
            }
        }
        public void AutoLoadTerritoryPresets(uint territoryId)
        {
            if (_lastLoadedTerritory == territoryId)
                return;

            _lastLoadedTerritory = territoryId;

            var presets = territoryId switch
            {
                1323 => new[] { "M10s.json" },
                1325 => new[] { "M11s.json" },
                1327 => new[] { "M12sP1.json", "M12sP2.json" },
                755 => new[] { "UMAD.json" },
                _ => Array.Empty<string>()
            };

            foreach (var preset in presets)
            {
                LoadEmbeddedPreset(preset);
            }
        }

        private void LoadEmbeddedPreset(string file)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = $"AetherBlackbox.Core.Mechanics.Presets.{file}";

            using var stream = assembly.GetManifestResourceStream(resourceName);

            if (stream == null)
            {
                Service.PluginLog.Warning($"Preset missing: {file}");
                return;
            }

            using var reader = new StreamReader(stream);
            var json = reader.ReadToEnd();

            var options = new JsonSerializerOptions { IncludeFields = true };
            options.Converters.Add(new JsonStringEnumConverter());

            var presets = JsonSerializer.Deserialize<List<CustomMechanicEntry>>(json, options);

            if (presets == null)
                return;

            foreach (var preset in presets)
            {
                AddEntry(preset);
            }
        }
    }
}
