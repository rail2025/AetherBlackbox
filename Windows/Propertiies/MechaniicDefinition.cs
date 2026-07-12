using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using AetherBlackbox.DrawingLogic;
using Lumina.Excel.Sheets;

namespace AetherBlackbox.Windows.Properties
{
    internal static class MechanicDefinition
    {
        private static string mechanicNameBuffer = "";
        private static int mechanicActionIdBuffer = 0;
        private static int mechanicSourceTypeIndex = 0;

        private static readonly string[] mechanicSourceTypes =
        {
            "Boss", "Player", "Environment"
        };

        private static string[] recentActionNames = Array.Empty<string>();
        private static uint[] recentActionIds = Array.Empty<uint>();
        private static int recentActionIndex = 0;

        public static void Draw(MainWindow mainWindow, List<BaseDrawable> selected, Plugin plugin)
        {
            if (selected == null || selected.Count != 1)
                return;

            float width = ImGui.GetContentRegionAvail().X;

            ImGui.Separator();
            ImGui.Text("Mechanic Definition");

            DrawFields(width);
            DrawScanner(mainWindow);
            DrawRecent(width);
            DrawSave(mainWindow, selected, plugin);
        }

        private static void DrawFields(float width)
        {
            ImGui.Text("Name");
            ImGui.SetNextItemWidth(width);
            ImGui.InputText("##MechName", ref mechanicNameBuffer, 64);

            ImGui.Text("Action ID");
            ImGui.SetNextItemWidth(width);
            ImGui.InputInt("##MechId", ref mechanicActionIdBuffer);

            ImGui.Text("Source Type");
            ImGui.SetNextItemWidth(width);
            ImGui.Combo("##MechSource", ref mechanicSourceTypeIndex,
                mechanicSourceTypes, mechanicSourceTypes.Length);
        }

        private static void DrawScanner(MainWindow mainWindow)
        {
            if (!ImGui.Button("Scan Last 5 Seconds"))
                return;

            var recording = mainWindow.ActiveDeathReplay?.ReplayData;
            if (recording?.Frames == null)
                return;

            float now = mainWindow.CurrentAbsoluteTime;
            float start = now - 5f;

            var names = new List<string>();
            var ids = new List<uint>();

            foreach (var frame in recording.Frames)
            {
                if (frame.TimeOffset < start || frame.TimeOffset > now)
                    continue;

                for (int i = 0; i < frame.Ids.Count; i++)
                {
                    uint entityId = frame.Ids[i];
                    string caster = "Unknown";

                    if (recording.Metadata != null &&
                        recording.Metadata.TryGetValue(entityId, out var meta))
                        caster = meta.Name;

                    var sheet = Service.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Action>();

                    if (frame.Actions != null &&
                        i < frame.Actions.Count &&
                        frame.Actions[i] != 0)
                    {
                        uint actionId = frame.Actions[i];

                        if (!ids.Contains(actionId))
                        {
                            var row = sheet?.GetRowOrDefault(actionId);
                            string name = row?.Name.ToString() ?? $"Action {actionId}";

                            names.Add($"[{caster}] {name}");
                            ids.Add(actionId);
                        }
                    }

                    if (frame.Casts != null && i < frame.Casts.Count)
                    {
                        var cast = frame.Casts[i];

                        if (cast.ActionId != 0 && !ids.Contains(cast.ActionId))
                        {
                            var row = sheet?.GetRowOrDefault(cast.ActionId);
                            string name = row?.Name.ToString() ?? $"Action {cast.ActionId}";

                            names.Add($"[{caster}] {name} (Cast)");
                            ids.Add(cast.ActionId);
                        }
                    }
                }
            }

            recentActionNames = names.ToArray();
            recentActionIds = ids.ToArray();
            recentActionIndex = 0;
        }

        private static void DrawRecent(float width)
        {
            if (recentActionNames.Length == 0)
                return;

            ImGui.Text("Recent Actions");
            ImGui.SetNextItemWidth(width);

            if (ImGui.Combo("##RecentActions",
                ref recentActionIndex,
                recentActionNames,
                recentActionNames.Length))
            {
                mechanicActionIdBuffer = (int)recentActionIds[recentActionIndex];
                mechanicNameBuffer = recentActionNames[recentActionIndex];
            }
        }

        private static void DrawSave(MainWindow mainWindow, List<BaseDrawable> selected, Plugin plugin)
        {
            if (!ImGui.Button("Save Definition"))
                return;

            var target = selected[0];

            var entry = new Core.Mechanics.CustomMechanicEntry
            {
                Name = mechanicNameBuffer,
                ActionId = (uint)mechanicActionIdBuffer,
                SourceType = (Core.Mechanics.MechanicSourceType)mechanicSourceTypeIndex,
                Color = target.Color,
                Thickness = target.Thickness,
                IsFilled = target.IsFilled,
                Duration = 2f
            };

            float unscale(float v) =>
                v / (ReplayRenderer.DefaultPixelsPerYard * ImGuiHelpers.GlobalScale);

            if (target is DrawableCircle c)
            {
                entry.Shape = Core.Mechanics.AoeShape.Circle;
                entry.Radius = unscale(c.Radius);
            }
            else if (target is DrawableDonut d)
            {
                entry.Shape = Core.Mechanics.AoeShape.Donut;
                entry.Radius = unscale(d.Radius);
                entry.InnerRadius = unscale(d.InnerRadius);
            }
            else if (target is DrawableRectangle r)
            {
                entry.Shape = Core.Mechanics.AoeShape.Rect;
                var g = r.GetGeometry();
                entry.Width = unscale(g.halfSize.X * 2f);
                entry.Radius = unscale(g.halfSize.Y * 2f);
            }
            else if (target is DrawablePie p)
            {
                entry.Shape = Core.Mechanics.AoeShape.Cone;
                entry.Radius = unscale(p.Radius);
                entry.Angle = p.SweepAngle * (180f / (float)Math.PI);
            }

            entry.ZoneId = mainWindow.ActiveDeathReplay?.TerritoryTypeId ?? 0;

            if (entry.ActionId == 0)
                return;

            entry.OriginFile = "CustomMechanics";
            plugin.PresetManager.AddEntry(entry);
            plugin.StorageService.UpdateEntry(entry);
            plugin.MechanicLibraryWindow.RefreshFiles();

            var drawables = mainWindow.PageManager.GetCurrentPageDrawables();
            drawables.Remove(target);
            mainWindow.SelectedDrawables.Remove(target);

            mainWindow.CanvasController.UndoManager.RecordAction(
                drawables,
                "Convert to Auto-Draw");
        }
    }
}