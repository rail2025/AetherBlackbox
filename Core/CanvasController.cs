using AetherBlackbox.DrawingLogic;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace AetherBlackbox.Core;

public class CanvasController
{
    private readonly PageManager pageManager;
    private readonly Plugin plugin;
    private readonly Configuration configuration;

    private readonly UndoManager undoManager;
    private readonly ShapeInteractionHandler shapeInteractionHandler;
    public readonly InPlaceTextEditor inPlaceTextEditor;

    private readonly Func<DrawMode> getDrawMode;
    private readonly Action<DrawMode> setDrawMode;
    private readonly Func<Vector4> getColor;
    private readonly Func<float> getThickness;
    private readonly Func<bool> getFilled;
    private readonly List<BaseDrawable> selectedItems;
    private readonly Func<BaseDrawable?> getHovered;
    private readonly Action<BaseDrawable?> setHovered;

    private bool isDrawing = false;
    private BaseDrawable? currentDrawing = null;
    private readonly List<DrawableLaser> ephemeralLasers = new();
    public BaseDrawable? GetCurrentDrawingObjectForPreview() => currentDrawing;
    public UndoManager UndoManager => undoManager;
    public ShapeInteractionHandler InteractionHandler => shapeInteractionHandler;

    public CanvasController(
        PageManager pageManager,
        Func<DrawMode> getDrawMode,
        Action<DrawMode> setDrawMode,
        Func<Vector4> getColor,
        Func<float> getThickness,
        Func<bool> getFilled,
        List<BaseDrawable> selectedItems,
        Func<BaseDrawable?> getHovered,
        Action<BaseDrawable?> setHovered,
        Configuration config,
        Plugin plugin)
    {
        this.pageManager = pageManager ?? throw new ArgumentNullException(nameof(pageManager));
        this.configuration = config ?? throw new ArgumentNullException(nameof(config));
        this.plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));

        this.getDrawMode = getDrawMode;
        this.setDrawMode = setDrawMode;
        this.getColor = getColor;
        this.getThickness = getThickness;
        this.getFilled = getFilled;
        this.selectedItems = selectedItems;
        this.getHovered = getHovered;
        this.setHovered = setHovered;

        this.undoManager = new UndoManager();
        this.shapeInteractionHandler = new ShapeInteractionHandler(
            plugin,
            undoManager,
            pageManager,
            guid => { },
            drawables =>
            {
                if (pageManager.IsLiveMode && drawables != null && drawables.Any())
                {
                    var payload = new Networking.NetworkPayload
                    {
                        PageIndex = pageManager.GetCurrentPageIndex(),
                        Action = Networking.PayloadActionType.UpdateObjects,
                        Data = Serialization.DrawableSerializer.SerializePageToBytes(drawables)
                    };
                    _ = plugin.NetworkManager.SendStateUpdateAsync(payload);
                }
            }
        );

        this.inPlaceTextEditor = new InPlaceTextEditor(plugin, undoManager, pageManager);
    }

    public BaseDrawable? GetCurrentPreview() => currentDrawing;

    public void Undo()
    {
        if (!undoManager.CanUndo()) return;

        var undone = undoManager.Undo();
        if (undone != null)
            pageManager.SetCurrentPageDrawables(undone);
    }

    private void ApplyTrackingAndTiming(BaseDrawable shape, float currentReplayTime, Vector2 effectivePos, Vector2 mousePosScreen, ReplayRenderer.ViewContext? viewContext, AetherBlackbox.Core.ReplayFrame? currentFrame)
    {
        shape.StartTime = currentReplayTime;
        shape.EndTime = currentReplayTime + 3f;
        shape.InitialLogicalPos = effectivePos;

        if (viewContext == null || currentFrame == null) return;

        var worldPos = ReplayRenderer.ScreenToWorld(mousePosScreen, viewContext);
        shape.InitialWorldPos = worldPos;

        for (int i = 0; i < currentFrame.Ids.Count; i++)
        {
            var entityPos = new Vector3(currentFrame.X[i], 0, currentFrame.Z[i]);
            if (Vector3.Distance(worldPos, entityPos) < 2f)
            {
                shape.IsEntityTracked = true;
                shape.TargetEntityId = currentFrame.Ids[i];
                shape.OffsetFromEntity = worldPos - entityPos;
                break;
            }
        }
    }

    public void ProcessCanvasInteraction(
        Vector2 mouseLogical, Vector2 mouseScreen, Vector2 canvasOrigin, ImDrawListPtr drawList,
        bool lmbDown, bool lmbClicked, bool lmbReleased, bool lmbDoubleClicked,
        ReplayRenderer.ViewContext? viewContext = null, float currentTime = 0f,
        ReplayFrame? currentFrame = null)
    {
        var mode = getDrawMode();
        var drawables = pageManager.GetCurrentPageDrawables();
        if (drawables == null) return;

        // Text editing on double-click
        if (lmbDoubleClicked && mode == DrawMode.Select)
        {
            var hovered = getHovered();
            if (hovered is DrawableText dt && !inPlaceTextEditor.IsCurrentlyEditing(dt))
            {
                inPlaceTextEditor.BeginEdit(dt, canvasOrigin, ImGuiHelpers.GlobalScale);
                return;
            }
        }

        if (mode == DrawMode.Select || mode == DrawMode.Eraser)
        {
            HandleSelectionOrEraser(mode, drawables, mouseLogical, mouseScreen, canvasOrigin, lmbClicked, lmbDown, lmbReleased, drawList);
            return;
        }

        if (mode == DrawMode.TextTool)
        {
            if (!lmbClicked) return;

            undoManager.RecordAction(drawables, "Add Text");
            var newText = new DrawableText(mouseLogical, "New Text", getColor(), 16f, 200f)
            {
                ReplayTime = currentTime
            };
            ApplyTrackingAndTiming(newText, currentTime, mouseLogical, mouseScreen, viewContext, currentFrame);
            drawables.Add(newText);

            foreach (var sel in selectedItems) sel.IsSelected = false;
            selectedItems.Clear();

            newText.IsSelected = true;
            selectedItems.Add(newText);
            setHovered(newText);

            inPlaceTextEditor.BeginEdit(newText, canvasOrigin, ImGuiHelpers.GlobalScale);
            setDrawMode(DrawMode.Select);
            return;
        }

        if (IsImagePlacementMode(mode))
        {
            HandleImagePlacement(mode, mouseLogical, mouseScreen, lmbClicked, drawables, currentTime, viewContext, currentFrame);
            return;
        }

        HandleShapeDrawing(mode, mouseLogical, mouseScreen, lmbDown, lmbClicked, lmbReleased, currentTime, viewContext, currentFrame);
    }

    private void HandleSelectionOrEraser(
        DrawMode mode,
        List<BaseDrawable> drawables,
        Vector2 mouseLogical,
        Vector2 mouseScreen,
        Vector2 canvasOrigin,
        bool lmbClicked,
        bool lmbDown,
        bool lmbReleased,
        ImDrawListPtr drawList)
    {
        BaseDrawable? singleSelection = selectedItems.Count == 1 ? selectedItems[0] : null;
        var hovered = getHovered();

        shapeInteractionHandler.ProcessInteractions(
            singleSelection, selectedItems, drawables,
            modeValue => 1, ref hovered,
            mouseLogical, mouseScreen, canvasOrigin,
            lmbClicked, lmbDown, lmbReleased, drawList);

        setHovered(hovered);

        if (mode == DrawMode.Eraser && lmbClicked && hovered != null)
        {
            undoManager.RecordAction(drawables, "Eraser");
            drawables.Remove(hovered);
            selectedItems.Remove(hovered);
            setHovered(null);
        }
    }

    private void HandleShapeDrawing(
        DrawMode mode,
        Vector2 logicalPos,
        Vector2 screenPos,
        bool lmbDown,
        bool lmbClicked,
        bool lmbReleased,
        float currentTime,
        ReplayRenderer.ViewContext? viewContext,
        ReplayFrame? currentFrame)
    {
        if (lmbDown)
        {
            if (!isDrawing && lmbClicked)
            {
                isDrawing = true;

                foreach (var sel in selectedItems) sel.IsSelected = false;
                selectedItems.Clear();
                setHovered(null);

                if (mode == DrawMode.Laser)
                {
                    var laser = new DrawableLaser(logicalPos, getColor(), getThickness())
                    {
                        ReplayTime = currentTime
                    };
                    ApplyTrackingAndTiming(laser, currentTime, logicalPos, screenPos, viewContext, currentFrame);
                    currentDrawing = laser;
                    ephemeralLasers.Add(laser);
                }
                else
                {
                    currentDrawing = CreateNewDrawingObject(mode, logicalPos, getColor(), getThickness(), getFilled?.Invoke() ?? false, currentTime);
                    if (currentDrawing != null)
                    {
                        undoManager.RecordAction(pageManager.GetCurrentPageDrawables(), $"Start Drawing {mode}");
                        currentDrawing.IsPreview = true;
                        ApplyTrackingAndTiming(currentDrawing, currentTime, logicalPos, screenPos, viewContext, currentFrame);
                    }
                }
            }

            if (isDrawing && currentDrawing != null)
            {
                switch (currentDrawing)
                {
                    case DrawableLaser laser:
                        laser.AddPoint(logicalPos);
                        if (pageManager.IsLiveMode && (DateTime.Now - laser.LastUpdateTime).TotalMilliseconds > 40)
                        {
                            var payload = new Networking.NetworkPayload
                            {
                                PageIndex = pageManager.GetCurrentPageIndex(),
                                Action = Networking.PayloadActionType.AddObjects,
                                Data = Serialization.DrawableSerializer.SerializePageToBytes(new List<BaseDrawable> { laser })
                            };
                            _ = plugin.NetworkManager.SendStateUpdateAsync(payload);
                            laser.LastUpdateTime = DateTime.Now;
                        }
                        break;

                    case DrawablePath path:
                        path.AddPoint(logicalPos);
                        break;

                    case DrawableDash dash:
                        dash.AddPoint(logicalPos);
                        break;

                    default:
                        currentDrawing.UpdatePreview(logicalPos);
                        break;
                }
            }
        }

        if (isDrawing && lmbReleased)
        {
            FinalizeCurrentDrawing();
        }
    }

    private void FinalizeCurrentDrawing()
    {
        if (currentDrawing == null)
        {
            isDrawing = false;
            return;
        }

        if (currentDrawing is DrawableLaser)
        {
            // lasers are ephemeral, delete after a delay
            var laserToDelete = (DrawableLaser)currentDrawing;
            _ = System.Threading.Tasks.Task.Run(async () =>
            {
                await System.Threading.Tasks.Task.Delay(600);
                if (pageManager.IsLiveMode)
                {
                    using var ms = new System.IO.MemoryStream();
                    using var writer = new System.IO.BinaryWriter(ms);
                    writer.Write(1);
                    writer.Write(laserToDelete.UniqueId.ToByteArray());

                    var payload = new Networking.NetworkPayload
                    {
                        PageIndex = pageManager.GetCurrentPageIndex(),
                        Action = Networking.PayloadActionType.DeleteObjects,
                        Data = ms.ToArray()
                    };
                    _ = plugin.NetworkManager?.SendStateUpdateAsync(payload);
                }
            });

            currentDrawing = null;
            isDrawing = false;
            return;
        }

        currentDrawing.IsPreview = false;

        var drawables = pageManager.GetCurrentPageDrawables();
        if (drawables != null)
        {
            bool valid = true;

            if (currentDrawing is DrawablePath p && p.PointsRelative.Count < 2) valid = false;
            if (currentDrawing is DrawableDash d && d.PointsRelative.Count < 2) valid = false;

            if (valid)
            {
                drawables.Add(currentDrawing);

                if (pageManager.IsLiveMode)
                {
                    var payload = new Networking.NetworkPayload
                    {
                        PageIndex = pageManager.GetCurrentPageIndex(),
                        Action = Networking.PayloadActionType.AddObjects,
                        Data = Serialization.DrawableSerializer.SerializePageToBytes(new List<BaseDrawable> { currentDrawing })
                    };
                    _ = plugin.NetworkManager?.SendStateUpdateAsync(payload);
                }
            }
            else
            {
                var undone = undoManager.Undo();
                if (undone != null) pageManager.SetCurrentPageDrawables(undone);
            }
        }

        currentDrawing = null;
        isDrawing = false;
    }

    private void HandleImagePlacement(
        DrawMode mode,
        Vector2 logicalPos,
        Vector2 screenPos,
        bool lmbClicked,
        List<BaseDrawable> drawables,
        float currentTime,
        ReplayRenderer.ViewContext? viewContext,
        ReplayFrame? currentFrame)
    {
        if (!lmbClicked) return;

        if (!ToolRegistry.Tools.TryGetValue(mode, out var meta) || string.IsNullOrEmpty(meta.CanvasImagePath)) return;

        var newImage = new DrawableImage(mode, meta.CanvasImagePath, logicalPos, meta.DefaultSize, Vector4.One)
        {
            ReplayTime = currentTime,
            IsPreview = false
        };

        ApplyTrackingAndTiming(newImage, currentTime, logicalPos, screenPos, viewContext, currentFrame);
        undoManager.RecordAction(drawables, "Place Image");
        drawables.Add(newImage);
    }

    private bool IsImagePlacementMode(DrawMode mode)
    {
        return ToolRegistry.Tools.TryGetValue(mode, out var meta) && meta.IsPlaceableImage;
    }

    public static BaseDrawable? CreateNewDrawingObject(DrawMode mode, Vector2 pos, Vector4 color, float thickness, bool filled, float time = 0f)
    {
        BaseDrawable? drawable = mode switch
        {
            DrawMode.Pen => new DrawablePath(pos, color, thickness),
            DrawMode.StraightLine => new DrawableStraightLine(pos, color, thickness),
            DrawMode.Dash => new DrawableDash(pos, color, thickness),
            DrawMode.Rectangle => new DrawableRectangle(pos, color, thickness, filled),
            DrawMode.Circle => new DrawableCircle(pos, color, thickness, filled),
            DrawMode.Donut => new DrawableDonut(pos, color, thickness, filled),
            DrawMode.Starburst => new DrawableStarburst(pos, new Vector4(1f, 0.6f, 0f, 0.6f), thickness, true),
            DrawMode.Arrow => new DrawableArrow(pos, color, thickness),
            DrawMode.Cone => new DrawableCone(pos, color, thickness, filled),
            DrawMode.Triangle => new DrawableTriangle(pos, color, thickness, filled),
            DrawMode.Pie => new DrawablePie(pos, color, thickness, filled),
            _ => null
        };

        if (drawable != null) drawable.ReplayTime = time;

        return drawable;
    }
}