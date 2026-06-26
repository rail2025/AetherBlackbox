using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using AetherBlackbox.DrawingLogic;

namespace AetherBlackbox.Windows.Properties
{
    internal static class ContextProperties
    {
        public static void Draw(
            MainWindow mainWindow,
            List<BaseDrawable> selected)
        {
            if (selected == null || selected.Count != 1)
                return;

            float width = ImGui.GetContentRegionAvail().X;

            DrawDonut(mainWindow, selected, width);
            DrawText(mainWindow, selected, width);
        }

        private static void DrawDonut(
            MainWindow mainWindow,
            List<BaseDrawable> selected,
            float width)
        {
            if (selected[0] is not DrawableDonut donut)
                return;

            ImGui.Separator();
            ImGui.Text("Donut Properties");

            float hole = donut.InnerRadius;
            float max = Math.Max(0f, donut.Radius - 5f);

            ImGui.SetNextItemWidth(width);

            if (ImGui.DragFloat("##HoleSize", ref hole, 0.5f, 0f, max, "Hole Size: %.1f"))
            {
                donut.InnerRadius = Math.Clamp(hole, 0f, max);
            }

            if (ImGui.IsItemDeactivatedAfterEdit())
            {
                mainWindow.CanvasController.UndoManager.RecordAction(
                    mainWindow.PageManager.GetCurrentPageDrawables(),
                    "Resize Donut Hole");

                mainWindow.CanvasController.InteractionHandler.CommitObjectChanges(
                    new List<BaseDrawable>(selected));
            }
        }

        private static void DrawText(
            MainWindow mainWindow,
            List<BaseDrawable> selected,
            float width)
        {
            if (selected[0] is not DrawableText text)
                return;

            ImGui.Separator();
            ImGui.Text("Text Properties");

            float size = text.FontSize;

            ImGui.SetNextItemWidth(width);

            if (ImGui.DragFloat("##FontSize", ref size, 0.5f, 1.0f, 200.0f, "Size: %.1f"))
            {
                text.FontSize = size;
            }

            if (ImGui.IsItemDeactivatedAfterEdit())
            {
                mainWindow.CanvasController.UndoManager.RecordAction(
                    mainWindow.PageManager.GetCurrentPageDrawables(),
                    "Change Font Size");

                mainWindow.CanvasController.InteractionHandler.CommitObjectChanges(
                    new List<BaseDrawable>(selected));
            }

            string raw = text.RawText;

            if (ImGui.InputTextMultiline(
                "##Content",
                ref raw,
                1024,
                new Vector2(-1, 100 * ImGuiHelpers.GlobalScale)))
            {
                text.RawText = raw;
            }

            if (ImGui.IsItemDeactivatedAfterEdit())
            {
                mainWindow.CanvasController.UndoManager.RecordAction(
                    mainWindow.PageManager.GetCurrentPageDrawables(),
                    "Edit Text");

                mainWindow.CanvasController.InteractionHandler.CommitObjectChanges(
                    new List<BaseDrawable>(selected));
            }
        }
    }
}