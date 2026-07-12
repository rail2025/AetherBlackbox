using AetherBlackbox.Core;
using AetherBlackbox.DrawingLogic;
using Dalamud.Interface.Utility;
using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace AetherBlackbox.Windows;

public partial class MainWindow
{
    private void DrawCanvas()
    {
        Vector2 canvasSizeForImGuiDrawing = ImGui.GetContentRegionAvail();
        currentCanvasDrawSize = canvasSizeForImGuiDrawing;
        if (canvasSizeForImGuiDrawing.X < 50f * ImGuiHelpers.GlobalScale) canvasSizeForImGuiDrawing.X = 50f * ImGuiHelpers.GlobalScale;
        if (canvasSizeForImGuiDrawing.Y < 50f * ImGuiHelpers.GlobalScale) canvasSizeForImGuiDrawing.Y = 50f * ImGuiHelpers.GlobalScale;
        ImDrawListPtr drawList = ImGui.GetWindowDrawList();
        Vector2 canvasOriginScreen = ImGui.GetCursorScreenPos();

        ReplayFrame? closestFrame = null;
        ReplayRecording? recording = null;
        Vector3 centerPos = new Vector3(100, 0, 100);
        float targetOffset = 0f;

        if (isReplayMode && ActiveDeathReplay != null && ActiveDeathReplay.ReplayData.Frames.Count > 0)
        {
            recording = ActiveDeathReplay.ReplayData;
            var deathTimeOffset = selectedPull != null ? (float)(ActiveDeathReplay.TimeOfDeath - selectedPull.StartTime).TotalSeconds : recording.Frames.Last().TimeOffset;
            targetOffset = deathTimeOffset + replayTimeOffset;
            closestFrame = GetClosestFrame(recording, targetOffset);

            if (cachedArenaCenter == null && recording.Frames.Count > 0 && recording.Frames[0].Ids.Count > 0)
            {
                var firstFrame = recording.Frames[0];
                Vector3 sum = Vector3.Zero;
                for (int i = 0; i < firstFrame.Ids.Count; i++) sum += new Vector3(firstFrame.X[i], 0, firstFrame.Z[i]);
                cachedArenaCenter = sum / firstFrame.Ids.Count;
            }
            centerPos = cachedArenaCenter ?? new Vector3(100, 0, 100);
        }

        ImGui.SetCursorScreenPos(canvasOriginScreen);

        ImGui.InvisibleButton("##CanvasInput", canvasSizeForImGuiDrawing);

        bool hovered = ImGui.IsItemHovered();
        bool active = ImGui.IsItemActive();

        if (hovered)
        {
            float wheel = ImGui.GetIO().MouseWheel;
            if (wheel != 0) canvasZoom = Math.Clamp(canvasZoom + (wheel * 0.1f), 0.1f, 5.0f);
        }

        bool isLMBDown = (hovered || active) && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        bool isLMBClicked = hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
        bool isLMBReleased = (hovered || active) && ImGui.IsMouseReleased(ImGuiMouseButton.Left);
        bool isLMBDoubleClicked = hovered && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left);
        bool isRMBDragging = (hovered || active) && ImGui.IsMouseDragging(ImGuiMouseButton.Right);

        if (IsDrawingMode)
        {
            var viewContext = new ReplayRenderer.ViewContext(canvasOriginScreen, currentCanvasDrawSize, centerPos, canvasZoom, canvasPanOffset);
            var mousePosScreen = ImGui.GetMousePos();
            var mousePosLogical = (mousePosScreen - canvasOriginScreen - canvasPanOffset) / ImGuiHelpers.GlobalScale;

            if (configuration.SnapToGrid && configuration.IsGridVisible && configuration.GridSize > 0)
            {
                float scaledGridSize = configuration.GridSize * ImGuiHelpers.GlobalScale;
                mousePosLogical.X = (float)Math.Round(mousePosLogical.X / scaledGridSize) * scaledGridSize;
                mousePosLogical.Y = (float)Math.Round(mousePosLogical.Y / scaledGridSize) * scaledGridSize;
                mousePosScreen = canvasOriginScreen + mousePosLogical;
            }

            canvasController.ProcessCanvasInteraction(
                mousePosLogical, mousePosScreen, canvasOriginScreen, drawList,
                isLMBDown, isLMBClicked, isLMBReleased, isLMBDoubleClicked,
                viewContext, targetOffset, closestFrame);
        }

        if (hovered || active || isLMBReleased)
        {
            if (isRMBDragging)
            {
                canvasPanOffset += ImGui.GetIO().MouseDelta;
            }

            if (!IsDrawingMode && active && !isRMBDragging)
            {
                // Pan (Drag)
                if (ImGui.IsMouseDragging(ImGuiMouseButton.Left))
                {
                    canvasPanOffset += ImGui.GetIO().MouseDelta;
                }
                // Click (Select) - Only if NOT dragging
                else if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && closestFrame != null && recording != null)
                {
                    selectedEntityId = 0;
                    float bestDist = float.MaxValue;

                    var effectiveCanvasCenter = (canvasOriginScreen + currentCanvasDrawSize / 2) + canvasPanOffset;
                    float effectiveScale = 8f * ImGuiHelpers.GlobalScale * canvasZoom;

                    for (int i = 0; i < closestFrame.Ids.Count; i++)
                    {
                        var id = closestFrame.Ids[i];
                        if (!recording.Metadata.TryGetValue(id, out var meta)) continue;

                        bool isBoss = meta.Type == EntityType.Boss;
                        bool isPlayer = meta.ClassJobId != 0;
                        bool isPet = meta.Type == EntityType.Pet;

                        if (!configuration.ShowReplayNpcs && !isBoss && !isPlayer && !isPet) continue;

                        if (i >= closestFrame.X.Count || i >= closestFrame.Z.Count) continue;
                        var entityPos = new Vector3(closestFrame.X[i], 0, closestFrame.Z[i]);
                        var relPos = entityPos - centerPos;
                        var screenX = effectiveCanvasCenter.X + (relPos.X * effectiveScale);
                        var screenY = effectiveCanvasCenter.Y + (relPos.Z * effectiveScale);

                        float dist = Vector2.Distance(ImGui.GetMousePos(), new Vector2(screenX, screenY));
                        if (dist < 25f * ImGuiHelpers.GlobalScale * canvasZoom && dist < bestDist)
                        {
                            bestDist = dist;
                            selectedEntityId = closestFrame.Ids[i];
                        }
                    }
                }
            }
        }

        drawList.AddRectFilled(canvasOriginScreen, canvasOriginScreen + canvasSizeForImGuiDrawing, ImGui.GetColorU32(new Vector4(0.15f, 0.15f, 0.17f, 1.0f)));
        if (configuration.IsGridVisible)
        {
            float scaledGridCellSize = configuration.GridSize * ImGuiHelpers.GlobalScale;
            if (scaledGridCellSize > 2)
            {
                var gridColor = ImGui.GetColorU32(new Vector4(0.3f, 0.3f, 0.3f, 1.0f));
                for (float x = scaledGridCellSize; x < canvasSizeForImGuiDrawing.X; x += scaledGridCellSize)
                    drawList.AddLine(new Vector2(canvasOriginScreen.X + x, canvasOriginScreen.Y), new Vector2(canvasOriginScreen.X + x, canvasOriginScreen.Y + canvasSizeForImGuiDrawing.Y), gridColor, Math.Max(1f, 1.0f * ImGuiHelpers.GlobalScale));
                for (float y = scaledGridCellSize; y < canvasSizeForImGuiDrawing.Y; y += scaledGridCellSize)
                    drawList.AddLine(new Vector2(canvasOriginScreen.X, canvasOriginScreen.Y + y), new Vector2(canvasOriginScreen.X + canvasSizeForImGuiDrawing.X, canvasOriginScreen.Y + y), gridColor, Math.Max(1f, 1.0f * ImGuiHelpers.GlobalScale));
            }
        }
        drawList.AddRect(canvasOriginScreen - Vector2.One, canvasOriginScreen + canvasSizeForImGuiDrawing + Vector2.One, ImGui.GetColorU32(new Vector4(0.4f, 0.4f, 0.45f, 1f)), 0f, ImDrawFlags.None, Math.Max(1f, 1.0f * ImGuiHelpers.GlobalScale));

        if (closestFrame != null && recording != null)
        {
            replayRenderer.Draw(
                drawList,
                recording,
                closestFrame,
                targetOffset,
                canvasOriginScreen,
                currentCanvasDrawSize,
                centerPos,
                ActiveDeathReplay.TerritoryTypeId,
                plugin.Configuration.ShowReplayNpcs,
                plugin.Configuration.ShowReplayHp,
                plugin.Configuration.AnonymizeNames,
                canvasZoom,
                canvasPanOffset,
                plugin.Configuration,
                plugin.PresetManager
            );

            var activeAoEs = AoeAutomator.GetActiveAoEs(recording, targetOffset, ActiveDeathReplay.TerritoryTypeId, plugin.PresetManager);
            new AoeBridge().SyncActiveAoEs(activeAoEs, pageManager);

            // Selection Circle
            if (selectedEntityId != 0)
            {
                int selIdx = closestFrame.Ids.IndexOf((uint)selectedEntityId);
                if (selIdx != -1 && closestFrame.X != null && selIdx < closestFrame.X.Count && closestFrame.Z != null && selIdx < closestFrame.Z.Count)
                {
                    var entityPos = new Vector3(closestFrame.X[selIdx], 0, closestFrame.Z[selIdx]);
                    var relPos = entityPos - centerPos;
                    var canvasCenter = (canvasOriginScreen + currentCanvasDrawSize / 2) + canvasPanOffset;
                    float scale = 8f * ImGuiHelpers.GlobalScale * canvasZoom;

                    var screenX = canvasCenter.X + (relPos.X * scale);
                    var screenY = canvasCenter.Y + (relPos.Z * scale);

                    drawList.AddCircle(new Vector2(screenX, screenY), 22f * ImGuiHelpers.GlobalScale, 0xFF00D7FF, 0, 3f);
                }
            }
        }

        // User Drawings
        ImGui.PushClipRect(canvasOriginScreen, canvasOriginScreen + canvasSizeForImGuiDrawing, true);
        var drawablesSnapshot = pageManager.GetCurrentPageDrawables()?.ToList();
        var renderViewContext = new ReplayRenderer.ViewContext(canvasOriginScreen, currentCanvasDrawSize, centerPos, canvasZoom, canvasPanOffset);

        if (drawablesSnapshot != null && drawablesSnapshot.Any())
        {
            var sortedDrawables = drawablesSnapshot.OrderBy(d => GetLayerPriority(d.ObjectDrawMode));
            foreach (var drawable in sortedDrawables)
            {
                drawable.DrawProjected(drawList, renderViewContext, closestFrame, targetOffset);
            }
        }
        canvasController.GetCurrentDrawingObjectForPreview()?.DrawProjected(drawList, renderViewContext, closestFrame, targetOffset);

        ImGui.PopClipRect();

        canvasController.inPlaceTextEditor?.DrawEditorUI();
    }

    private int GetLayerPriority(DrawMode mode)
    {
        string modeName = mode.ToString();

        if (modeName == nameof(DrawMode.TextTool)) return 10;
        if (modeName == nameof(DrawMode.EmojiImage)) return 6;
        if (modeName == nameof(DrawMode.Image)) return 0;

        if (modeName.StartsWith("Waymark") ||
            modeName.StartsWith("Role") ||
            modeName.StartsWith("Party") ||
            modeName.StartsWith("Dot") ||
            modeName.EndsWith("Icon") ||
            modeName is "SquareImage" or "CircleMarkImage" or "TriangleImage" or "PlusImage")
        {
            return 5;
        }

        if (modeName.EndsWith("AoEImage") ||
            modeName is "BossImage" or "FlareImage" or "LineStackImage" or "SpreadImage" or "StackImage")
        {
            return 3;
        }

        return mode switch
        {
            DrawMode.Pen or DrawMode.StraightLine or DrawMode.Rectangle or DrawMode.Circle or
            DrawMode.Arrow or DrawMode.Cone or DrawMode.Dash or DrawMode.Donut => 2,
            _ => 1,
        };
    }
}