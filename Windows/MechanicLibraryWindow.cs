using AetherBlackbox.Core.Mechanics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using System;
using System.Numerics;

namespace AetherBlackbox.Windows
{
    public class MechanicLibraryWindow : Window, IDisposable
    {
        private readonly Plugin plugin;
        private string targetFileNameBuffer = "";

        public MechanicLibraryWindow(Plugin plugin) : base("Mechanic Library###MechanicLibrary")
        {
            this.plugin = plugin;
            this.SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(300, 200), MaximumSize = new Vector2(float.MaxValue, float.MaxValue) };
        }

        public void Dispose() { }

        public override void Draw()
        {
            ImGui.Text("Manage Permanent Files:");
            ImGui.InputText("File Name", ref targetFileNameBuffer, 64);

            if (ImGui.Button("Load File"))
            {
                if (!string.IsNullOrWhiteSpace(targetFileNameBuffer))
                {
                    var loaded = plugin.StorageService.Load(targetFileNameBuffer);
                    foreach (var entry in loaded)
                    {
                        plugin.PresetManager.AddEntry(entry);
                    }
                }
            }

            ImGui.SameLine();

            if (ImGui.Button("Delete File"))
            {
                if (!string.IsNullOrWhiteSpace(targetFileNameBuffer))
                {
                    plugin.StorageService.Delete(targetFileNameBuffer);
                    // Forcefully clear memory of any rules that might have been part of this file
                    plugin.PresetManager.PurgeReferences(e => true);
                    targetFileNameBuffer = "";
                }
            }
        }
    }
}