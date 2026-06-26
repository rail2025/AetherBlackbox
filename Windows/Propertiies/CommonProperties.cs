using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using AetherBlackbox.DrawingLogic;

namespace AetherBlackbox.Windows.Properties
{
    internal static class CommonProperties
    {
        public static void Draw(
            MainWindow mainWindow,
            List<BaseDrawable> selected)
        {
            if (selected == null || selected.Count == 0)
                return;

            float availableWidth = ImGui.GetContentRegionAvail().X;

            DrawLock(mainWindow, selected);

            ImGui.Separator();

            DrawColor(mainWindow, selected, availableWidth);

            DrawOpacity(mainWindow, selected, availableWidth);

            ImGui.Separator();

            DrawThickness(mainWindow, selected, availableWidth);

            ImGui.Separator();

            DrawFill(mainWindow, selected);
        }

        private static void DrawLock(MainWindow mainWindow, List<BaseDrawable> selected)
        {
            bool isLocked = selected.All(d => d.IsLocked);

            if (ImGui.Checkbox("Locked", ref isLocked))
            {
                foreach (var d in selected)
                    d.IsLocked = isLocked;

                mainWindow.CanvasController.UndoManager.RecordAction(
                    mainWindow.PageManager.GetCurrentPageDrawables(),
                    isLocked ? "Lock Objects" : "Unlock Objects");

                mainWindow.CanvasController.InteractionHandler.CommitObjectChanges(
                    new List<BaseDrawable>(selected));
            }

            ImGui.SameLine();
            ImGui.TextDisabled("(?)");

            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Locked objects cannot be selected or moved on the canvas.");
        }

        private static void DrawColor(MainWindow mainWindow, List<BaseDrawable> selected, float availableWidth)
        {
            var palette = PropertyConstants.ColorPalette;

            float itemSpacing = ImGui.GetStyle().ItemSpacing.X;
            int colorsPerRow = 5;

            float size = (availableWidth - (itemSpacing * (colorsPerRow - 1))) / colorsPerRow;
            var btnSize = new Vector2(size, size);

            for (int i = 0; i < palette.Length; i++)
            {
                if (i > 0 && i % colorsPerRow != 0)
                    ImGui.SameLine();

                ImGui.PushID(i);

                if (ImGui.ColorButton($"##color{i}", palette[i],
                    ImGuiColorEditFlags.NoTooltip | ImGuiColorEditFlags.NoAlpha,
                    btnSize))
                {
                    foreach (var d in selected)
                        d.Color = palette[i];

                    mainWindow.CanvasController.UndoManager.RecordAction(
                        mainWindow.PageManager.GetCurrentPageDrawables(),
                        "Change Color");

                    mainWindow.CanvasController.InteractionHandler.CommitObjectChanges(
                        new List<BaseDrawable>(selected));
                }

                ImGui.PopID();

                if (selected[0].Color == palette[i])
                {
                    ImGui.GetWindowDrawList().AddRect(
                        ImGui.GetItemRectMin(),
                        ImGui.GetItemRectMax(),
                        ImGui.GetColorU32(new Vector4(1, 1, 0, 1)),
                        0,
                        ImDrawFlags.None,
                        2f);
                }
            }
        }

        private static void DrawOpacity(MainWindow mainWindow, List<BaseDrawable> selected, float availableWidth)
        {
            float alpha = selected[0].Color.W;

            ImGui.SetNextItemWidth(availableWidth);

            if (ImGui.SliderFloat("##Opacity", ref alpha, 0.0f, 1.0f, "Opacity: %.2f"))
            {
                foreach (var d in selected)
                {
                    var c = d.Color;
                    c.W = alpha;
                    d.Color = c;
                }
            }

            if (ImGui.IsItemDeactivatedAfterEdit())
            {
                mainWindow.CanvasController.UndoManager.RecordAction(
                    mainWindow.PageManager.GetCurrentPageDrawables(),
                    "Change Opacity");

                mainWindow.CanvasController.InteractionHandler.CommitObjectChanges(
                    new List<BaseDrawable>(selected));
            }
        }

        private static void DrawThickness(MainWindow mainWindow, List<BaseDrawable> selected, float availableWidth)
        {
            var presets = PropertyConstants.ThicknessPresets;
            float spacing = ImGui.GetStyle().ItemSpacing.X;

            float btnWidth = (availableWidth - spacing * (presets.Length - 1)) / presets.Length;

            foreach (var t in presets)
            {
                if (t != presets[0])
                    ImGui.SameLine();

                bool active = Math.Abs(selected[0].Thickness - t) < 0.01f;

                if (ImGui.Button($"{t:0}##thick{t}", new Vector2(btnWidth, 0)))
                {
                    foreach (var d in selected)
                    {
                        d.Thickness = t;

                        if (d is DrawableArrow arrow)
                            arrow.UpdateArrowheadSize();
                    }

                    mainWindow.CanvasController.UndoManager.RecordAction(
                        mainWindow.PageManager.GetCurrentPageDrawables(),
                        "Change Thickness");

                    mainWindow.CanvasController.InteractionHandler.CommitObjectChanges(
                        new List<BaseDrawable>(selected));
                }

                if (active)
                {
                    ImGui.GetWindowDrawList().AddRect(
                        ImGui.GetItemRectMin(),
                        ImGui.GetItemRectMax(),
                        ImGui.GetColorU32(new Vector4(1, 1, 0, 1)),
                        0,
                        ImDrawFlags.None,
                        2f);
                }
            }
        }

        private static void DrawFill(MainWindow mainWindow, List<BaseDrawable> selected)
        {
            bool filled = selected[0].IsFilled;

            if (ImGui.Checkbox("Filled", ref filled))
            {
                foreach (var d in selected)
                {
                    d.IsFilled = filled;

                    if (d.Color.W < 1.0f || filled)
                    {
                        var c = d.Color;
                        c.W = filled ? 0.4f : 1.0f;
                        d.Color = c;
                    }
                }

                mainWindow.CanvasController.UndoManager.RecordAction(
                    mainWindow.PageManager.GetCurrentPageDrawables(),
                    "Change Fill");

                mainWindow.CanvasController.InteractionHandler.CommitObjectChanges(
                    new List<BaseDrawable>(selected));
            }
        }
    }
}