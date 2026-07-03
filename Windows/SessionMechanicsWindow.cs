using AetherBlackbox.Core.Mechanics;
using AetherBlackbox.DrawingLogic;
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
        private CustomMechanicEntry selectedEntry;

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
            if (ImGui.BeginListBox("##SessionList", new Vector2(-1, 150)))
            {
                foreach (var entry in cachedMemory)
                {
                    bool isSelected = selectedEntry == entry;
                    if (ImGui.Selectable($"- {entry.Name} (Action ID: {entry.ActionId})", isSelected))
                    {
                        selectedEntry = entry;

                        var targetMode = entry.Shape switch
                        {
                            AoeShape.Circle => DrawMode.Circle,
                            AoeShape.Cone => DrawMode.Cone,
                            AoeShape.Rect => DrawMode.Rectangle,
                            AoeShape.Donut => DrawMode.Donut,
                            AoeShape.Pie => DrawMode.Pie,
                            _ => DrawMode.Circle
                        };

                        plugin.ToolbarWindow.SetActiveTool(targetMode);
                        plugin.PropertiesWindow.IsOpen = true;

                        // Assumes a roughly centered default spawn point on the canvas
                        plugin.MainWindow.CanvasController?.SpawnMechanicPreview(entry, new Vector2(500, 500));
                    }
                }
                ImGui.EndListBox();
            }

            ImGui.Separator();

            if (selectedEntry != null)
            {
                ImGui.TextColored(new Vector4(0, 1, 0, 1), $"Currently Editing: {selectedEntry.Name}");
                ImGui.TextWrapped("Modify the shape using the Main Canvas and Properties Window. When finished, save the changes below.");

                if (ImGui.Button("Save Canvas Modifications to Mechanic"))
                {
                    var selectedObj = plugin.MainWindow.CanvasController?.GetSingleSelectedItem();
                    if (selectedObj != null)
                    {
                        var type = selectedObj.GetType();

                        var colorProp = type.GetProperty("Color");
                        if (colorProp != null) selectedEntry.Color = (Vector4)colorProp.GetValue(selectedObj)!;

                        var thicknessProp = type.GetProperty("Thickness");
                        if (thicknessProp != null) selectedEntry.Thickness = (float)thicknessProp.GetValue(selectedObj)!;

                        var filledProp = type.GetProperty("IsFilled");
                        if (filledProp != null) selectedEntry.IsFilled = (bool)filledProp.GetValue(selectedObj)!;

                        var radiusProp = type.GetProperty("Radius") ?? type.GetProperty("OuterRadius") ?? type.GetProperty("Height");
                        if (radiusProp != null) selectedEntry.Radius = (float)radiusProp.GetValue(selectedObj)!;

                        var widthProp = type.GetProperty("Width");
                        if (widthProp != null) selectedEntry.Width = (float)widthProp.GetValue(selectedObj)!;

                        var innerProp = type.GetProperty("InnerRadius");
                        if (innerProp != null) selectedEntry.InnerRadius = (float)innerProp.GetValue(selectedObj)!;

                        var angleProp = type.GetProperty("Angle") ?? type.GetProperty("SweepAngle");
                        if (angleProp != null) selectedEntry.Angle = (float)angleProp.GetValue(selectedObj)!;

                        if (string.IsNullOrEmpty(selectedEntry.OriginFile)) selectedEntry.OriginFile = "CustomMechanics";
                        plugin.StorageService.UpdateEntry(selectedEntry);
                        plugin.MechanicLibraryWindow.RefreshFiles();
                    }
                }
            }
            else
            {
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
}