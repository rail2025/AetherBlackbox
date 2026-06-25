using System;
using System.Collections.Generic;
using System.Text;

namespace AetherBlackbox.Core.Mechanics
{
    public class PresetManager
    {
        private readonly List<CustomMechanicEntry> activeMemory = new();
        public event Action? OnMemoryChanged;

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
    }
}
