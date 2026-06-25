using AetherBlackbox.Core.Mechanics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace AetherBlackbox.Windows
{
    public class MechanicLibraryWindow : Window, IDisposable
    {
        private readonly Plugin plugin;
        private string targetFileNameBuffer = "";
        private string tagFilterBuffer = "";
        private List<string> availableFiles = new();
        private int selectedFileIndex = -1;

        public MechanicLibraryWindow(Plugin plugin) : base("Mechanic Library###MechanicLibrary")
        {
            this.plugin = plugin;
            this.SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(300, 200), MaximumSize = new Vector2(float.MaxValue, float.MaxValue) };
            RefreshFiles();
        }
        public void RefreshFiles()
        {
            availableFiles = plugin.StorageService.GetAvailableFiles();
            selectedFileIndex = -1;
        }

        public void Dispose() { }

        public override void Draw()
        {
            ImGui.Text("Manage Permanent Files");
            ImGui.InputText("Filter by Tag", ref tagFilterBuffer, 64);
            ImGui.Separator();

            if (ImGui.BeginListBox("##SavedFilesList", new Vector2(-1, 150)))
            {
                for (int i = 0; i < availableFiles.Count; i++)
                {
                    bool isSelected = (selectedFileIndex == i);
                    if (ImGui.Selectable(availableFiles[i], isSelected))
                    {
                        selectedFileIndex = i;
                        targetFileNameBuffer = availableFiles[i];
                    }
                    if (isSelected) ImGui.SetItemDefaultFocus();
                }
                ImGui.EndListBox();
            }

            ImGui.Separator();
            ImGui.InputText("File Name", ref targetFileNameBuffer, 64);

            if (ImGui.Button("Load File"))
            {
                if (!string.IsNullOrWhiteSpace(targetFileNameBuffer))
                {
                    var loaded = plugin.StorageService.Load(targetFileNameBuffer);

                    var filteredLoad = string.IsNullOrWhiteSpace(tagFilterBuffer)
                        ? loaded
                        : loaded.Where(e => e.Tags != null && e.Tags.Contains(tagFilterBuffer));

                    foreach (var entry in filteredLoad)
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
                    plugin.PresetManager.PurgeReferences(e => e.OriginFile == targetFileNameBuffer);
                    targetFileNameBuffer = "";
                    RefreshFiles();
                }
            }
        }
    }
}