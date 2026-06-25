using AetherBlackbox.Core.Mechanics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text.Json;

namespace AetherBlackbox.Windows
{
    public class SessionMechanicsWindow : Window, IDisposable
    {
        private readonly Plugin plugin;
        private string saveNameBuffer = "";
        private List<CustomMechanicEntry> cachedMemory = new();

        public SessionMechanicsWindow(Plugin plugin) : base("Active Session Mechanics###SessionMechanics")
        {
            this.plugin = plugin;
            this.SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(300, 400), MaximumSize = new Vector2(float.MaxValue, float.MaxValue) };

            this.plugin.PresetManager.OnMemoryChanged += RefreshList;
            RefreshList();
        }

        public void Dispose()
        {
            this.plugin.PresetManager.OnMemoryChanged -= RefreshList;
        }

        private void RefreshList()
        {
            cachedMemory = new List<CustomMechanicEntry>(plugin.PresetManager.ActiveMemory);
        }

        public override void Draw()
        {
            if (ImGui.Button("Import from Clipboard"))
            {
                try
                {
                    string clipboard = ImGui.GetClipboardText();
                    var entry = JsonSerializer.Deserialize<CustomMechanicEntry>(clipboard);
                    if (entry != null && PresetValidator.IsValid(entry))
                    {
                        plugin.PresetManager.AddEntry(entry);
                    }
                }
                catch
                {
                    // Fail silently to prevent bad imports from crashing the thread
                }
            }

            ImGui.Separator();

            ImGui.Text("Active Memory:");
            foreach (var entry in cachedMemory)
            {
                ImGui.Text($"- {entry.Name} (Action ID: {entry.ActionId})");
            }

            ImGui.Separator();

            ImGui.InputText("Save File Name", ref saveNameBuffer, 64);
            if (ImGui.Button("Commit to Disk"))
            {
                if (!string.IsNullOrWhiteSpace(saveNameBuffer))
                {
                    plugin.StorageService.Save(saveNameBuffer, new List<CustomMechanicEntry>(plugin.PresetManager.ActiveMemory));
                    saveNameBuffer = "";
                    plugin.MechanicLibraryWindow.RefreshFiles();
                }
            }
        }
    }
}