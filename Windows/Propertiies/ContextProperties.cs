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
            DrawCircle(mainWindow, selected, width);
            DrawPie(mainWindow, selected, width);
            DrawStarburst(mainWindow, selected, width);
            DrawRectangle(mainWindow, selected, width);
            DrawCone(mainWindow, selected, width);
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
        private static void DrawCircle(MainWindow mainWindow, List<BaseDrawable> selected, float width)
        {
            if (selected[0] is not DrawableCircle circle) return;

            ImGui.Separator();
            ImGui.Text("Circle Properties");

            float radius = circle.Radius;
            ImGui.SetNextItemWidth(width);
            if (ImGui.DragFloat("##CircleRadius", ref radius, 0.5f, 0f, 2000f, "Radius: %.1f"))
                circle.Radius = Math.Max(0f, radius);

            if (ImGui.IsItemDeactivatedAfterEdit())
            {
                mainWindow.CanvasController.UndoManager.RecordAction(mainWindow.PageManager.GetCurrentPageDrawables(), "Resize Circle");
                mainWindow.CanvasController.InteractionHandler.CommitObjectChanges(new List<BaseDrawable>(selected));
            }
        }

        private static void DrawPie(MainWindow mainWindow, List<BaseDrawable> selected, float width)
        {
            if (selected[0] is not DrawablePie pie) return;

            ImGui.Separator();
            ImGui.Text("Pie Properties");

            float radius = pie.Radius;
            float rotDeg = pie.RotationAngle * (180f / MathF.PI);
            float sweepDeg = pie.SweepAngle * (180f / MathF.PI);

            ImGui.SetNextItemWidth(width);
            if (ImGui.DragFloat("##PieRadius", ref radius, 0.5f, 0f, 2000f, "Radius: %.1f"))
                pie.Radius = Math.Max(0f, radius);

            if (ImGui.IsItemDeactivatedAfterEdit())
            {
                mainWindow.CanvasController.UndoManager.RecordAction(mainWindow.PageManager.GetCurrentPageDrawables(), "Resize Pie");
                mainWindow.CanvasController.InteractionHandler.CommitObjectChanges(new List<BaseDrawable>(selected));
            }

            ImGui.SetNextItemWidth(width);
            if (ImGui.DragFloat("##PieRot", ref rotDeg, 1f, -360f, 360f, "Rotation: %.1f deg"))
                pie.RotationAngle = rotDeg * (MathF.PI / 180f);

            if (ImGui.IsItemDeactivatedAfterEdit())
            {
                mainWindow.CanvasController.UndoManager.RecordAction(mainWindow.PageManager.GetCurrentPageDrawables(), "Rotate Pie");
                mainWindow.CanvasController.InteractionHandler.CommitObjectChanges(new List<BaseDrawable>(selected));
            }

            ImGui.SetNextItemWidth(width);
            if (ImGui.DragFloat("##PieSweep", ref sweepDeg, 1f, 0f, 360f, "Angle: %.1f deg"))
                pie.SweepAngle = sweepDeg * (MathF.PI / 180f);

            if (ImGui.IsItemDeactivatedAfterEdit())
            {
                mainWindow.CanvasController.UndoManager.RecordAction(mainWindow.PageManager.GetCurrentPageDrawables(), "Change Pie Angle");
                mainWindow.CanvasController.InteractionHandler.CommitObjectChanges(new List<BaseDrawable>(selected));
            }
        }

        private static void DrawStarburst(MainWindow mainWindow, List<BaseDrawable> selected, float width)
        {
            if (selected[0] is not DrawableStarburst sb) return;

            ImGui.Separator();
            ImGui.Text("Starburst Properties");

            float radius = sb.Radius;
            float sbWidth = sb.Width;
            float rotDeg = sb.RotationAngle * (180f / MathF.PI);

            ImGui.SetNextItemWidth(width);
            if (ImGui.DragFloat("##SBRadius", ref radius, 0.5f, 0f, 2000f, "Radius: %.1f"))
                sb.Radius = Math.Max(0f, radius);

            if (ImGui.IsItemDeactivatedAfterEdit())
            {
                mainWindow.CanvasController.UndoManager.RecordAction(mainWindow.PageManager.GetCurrentPageDrawables(), "Resize Starburst");
                mainWindow.CanvasController.InteractionHandler.CommitObjectChanges(new List<BaseDrawable>(selected));
            }

            ImGui.SetNextItemWidth(width);
            if (ImGui.DragFloat("##SBWidth", ref sbWidth, 0.5f, 1f, 2000f, "Width: %.1f"))
                sb.Width = Math.Max(1f, sbWidth);

            if (ImGui.IsItemDeactivatedAfterEdit())
            {
                mainWindow.CanvasController.UndoManager.RecordAction(mainWindow.PageManager.GetCurrentPageDrawables(), "Resize Starburst Width");
                mainWindow.CanvasController.InteractionHandler.CommitObjectChanges(new List<BaseDrawable>(selected));
            }

            ImGui.SetNextItemWidth(width);
            if (ImGui.DragFloat("##SBRot", ref rotDeg, 1f, -360f, 360f, "Rotation: %.1f deg"))
                sb.RotationAngle = rotDeg * (MathF.PI / 180f);

            if (ImGui.IsItemDeactivatedAfterEdit())
            {
                mainWindow.CanvasController.UndoManager.RecordAction(mainWindow.PageManager.GetCurrentPageDrawables(), "Rotate Starburst");
                mainWindow.CanvasController.InteractionHandler.CommitObjectChanges(new List<BaseDrawable>(selected));
            }
        }

        private static void DrawRectangle(MainWindow mainWindow, List<BaseDrawable> selected, float canvasWidth)
        {
            if (selected[0] is not DrawableRectangle rect) return;

            ImGui.Separator();
            ImGui.Text("Rectangle Properties");

            var (center, halfSize) = rect.GetGeometry();
            float w = halfSize.X * 2f;
            float h = halfSize.Y * 2f;
            float rotDeg = rect.RotationAngle * (180f / MathF.PI);

            ImGui.SetNextItemWidth(canvasWidth);
            if (ImGui.DragFloat("##RectWidth", ref w, 1f, 1f, 2000f, "Width: %.1f"))
            {
                rect.StartPointRelative = new Vector2(center.X - (w / 2f), center.Y - (h / 2f));
                rect.EndPointRelative = new Vector2(center.X + (w / 2f), center.Y + (h / 2f));
            }

            if (ImGui.IsItemDeactivatedAfterEdit())
            {
                mainWindow.CanvasController.UndoManager.RecordAction(mainWindow.PageManager.GetCurrentPageDrawables(), "Resize Rectangle Width");
                mainWindow.CanvasController.InteractionHandler.CommitObjectChanges(new List<BaseDrawable>(selected));
            }

            ImGui.SetNextItemWidth(canvasWidth);
            if (ImGui.DragFloat("##RectHeight", ref h, 1f, 1f, 2000f, "Height: %.1f"))
            {
                rect.StartPointRelative = new Vector2(center.X - (w / 2f), center.Y - (h / 2f));
                rect.EndPointRelative = new Vector2(center.X + (w / 2f), center.Y + (h / 2f));
            }

            if (ImGui.IsItemDeactivatedAfterEdit())
            {
                mainWindow.CanvasController.UndoManager.RecordAction(mainWindow.PageManager.GetCurrentPageDrawables(), "Resize Rectangle Height");
                mainWindow.CanvasController.InteractionHandler.CommitObjectChanges(new List<BaseDrawable>(selected));
            }

            ImGui.SetNextItemWidth(canvasWidth);
            if (ImGui.DragFloat("##RectRot", ref rotDeg, 1f, -360f, 360f, "Rotation: %.1f deg"))
                rect.RotationAngle = rotDeg * (MathF.PI / 180f);

            if (ImGui.IsItemDeactivatedAfterEdit())
            {
                mainWindow.CanvasController.UndoManager.RecordAction(mainWindow.PageManager.GetCurrentPageDrawables(), "Rotate Rectangle");
                mainWindow.CanvasController.InteractionHandler.CommitObjectChanges(new List<BaseDrawable>(selected));
            }
        }

        private static void DrawCone(MainWindow mainWindow, List<BaseDrawable> selected, float width)
        {
            if (selected[0] is not DrawableCone cone) return;

            ImGui.Separator();
            ImGui.Text("Cone Properties");

            Vector2 dir = cone.BaseCenterRelative - cone.ApexRelative;
            float currentLength = dir.Length();
            float rotDeg = cone.RotationAngle * (180f / MathF.PI);

            ImGui.SetNextItemWidth(width);
            if (ImGui.DragFloat("##ConeLength", ref currentLength, 1f, 1f, 2000f, "Length: %.1f"))
            {
                if (currentLength > 0.001f)
                {
                    Vector2 normDir = dir.LengthSquared() > 0 ? Vector2.Normalize(dir) : new Vector2(0, 1);
                    cone.BaseCenterRelative = cone.ApexRelative + (normDir * currentLength);
                }
            }

            if (ImGui.IsItemDeactivatedAfterEdit())
            {
                mainWindow.CanvasController.UndoManager.RecordAction(mainWindow.PageManager.GetCurrentPageDrawables(), "Resize Cone");
                mainWindow.CanvasController.InteractionHandler.CommitObjectChanges(new List<BaseDrawable>(selected));
            }

            ImGui.SetNextItemWidth(width);
            if (ImGui.DragFloat("##ConeRot", ref rotDeg, 1f, -360f, 360f, "Rotation: %.1f deg"))
                cone.RotationAngle = rotDeg * (MathF.PI / 180f);

            if (ImGui.IsItemDeactivatedAfterEdit())
            {
                mainWindow.CanvasController.UndoManager.RecordAction(mainWindow.PageManager.GetCurrentPageDrawables(), "Rotate Cone");
                mainWindow.CanvasController.InteractionHandler.CommitObjectChanges(new List<BaseDrawable>(selected));
            }
        }
    }
}