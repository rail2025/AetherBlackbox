using AetherBlackbox.DrawingLogic;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace AetherBlackbox.Core
{
    public class CanvasController
    {
        private readonly PageManager pageManager;
        private readonly Plugin plugin;
        private readonly Configuration configuration;

        private readonly UndoManager undoManager;
        private readonly ShapeInteractionHandler shapeInteractionHandler;
        public readonly InPlaceTextEditor inPlaceTextEditor;
        public UndoManager UndoManager => undoManager;
        public ShapeInteractionHandler InteractionHandler => shapeInteractionHandler;
        private readonly Func<DrawMode> getDrawModeFunc;
        private readonly Action<DrawMode> setDrawModeAction;
        private readonly Func<Vector4> getBrushColorFunc;
        private readonly Func<float> getBrushThicknessFunc;
        private readonly Func<bool> getShapeFilledFunc;
        private readonly List<BaseDrawable> selectedDrawablesRef;
        private readonly Func<BaseDrawable?> getHoveredDrawableDelegate;
        private readonly Action<BaseDrawable?> setHoveredDrawableDelegate;

        public CanvasController(
            PageManager pageManagerInstance,
            Func<DrawMode> getDrawModeFunc,
            Action<DrawMode> setDrawModeAction,
            Func<Vector4> getBrushColorFunc,
            Func<float> getBrushThicknessFunc,
            Func<bool> getShapeFilledFunc,
            List<BaseDrawable> selectedDrawablesRef,
            Func<BaseDrawable?> getHoveredDrawableDelegate,
            Action<BaseDrawable?> setHoveredDrawableDelegate,
            Configuration config,
            Plugin pluginInstance)
        {
            this.pageManager = pageManagerInstance ?? throw new ArgumentNullException(nameof(pageManagerInstance));
            this.configuration = config ?? throw new ArgumentNullException(nameof(config));
            this.plugin = pluginInstance ?? throw new ArgumentNullException(nameof(pluginInstance));

            this.getDrawModeFunc = getDrawModeFunc;
            this.setDrawModeAction = setDrawModeAction;
            this.getBrushColorFunc = getBrushColorFunc;
            this.getBrushThicknessFunc = getBrushThicknessFunc;
            this.getShapeFilledFunc = getShapeFilledFunc;
            this.selectedDrawablesRef = selectedDrawablesRef;
            this.getHoveredDrawableDelegate = getHoveredDrawableDelegate;
            this.setHoveredDrawableDelegate = setHoveredDrawableDelegate;

            this.undoManager = new UndoManager();
            this.shapeInteractionHandler = new ShapeInteractionHandler(
                plugin,
                this.undoManager,
                this.pageManager,
                (guid) => { },
                (drawables) =>
                {
                    if (this.pageManager.IsLiveMode && drawables != null && drawables.Any())
                    {
                        var payload = new AetherBlackbox.Networking.NetworkPayload
                        {
                            PageIndex = this.pageManager.GetCurrentPageIndex(),
                            Action = AetherBlackbox.Networking.PayloadActionType.UpdateObjects,
                            Data = AetherBlackbox.Serialization.DrawableSerializer.SerializePageToBytes(drawables)
                        };
                        _ = plugin.NetworkManager.SendStateUpdateAsync(payload);
                    }
                }
            );
            this.inPlaceTextEditor = new InPlaceTextEditor(plugin, this.undoManager, this.pageManager);
        }
        private bool isDrawingOnCanvas = false;
        private BaseDrawable? currentDrawingObjectInternal = null;
        private readonly List<DrawableLaser> _ephemeralDrawables = new();

        public BaseDrawable? GetCurrentDrawingObjectForPreview() => currentDrawingObjectInternal;

        private void ApplyTrackingAndTiming(BaseDrawable shape, float currentReplayTime, Vector2 effectivePos, Vector2 mousePosScreen, ReplayRenderer.ViewContext? viewContext, AetherBlackbox.Core.ReplayFrame? currentFrame)
        {
            shape.StartTime = currentReplayTime;
            shape.EndTime = currentReplayTime + 3.0f;
            shape.InitialLogicalPos = effectivePos;

            if (viewContext != null && currentFrame != null)
            {
                Vector3 clickWorldPos = ReplayRenderer.ScreenToWorld(mousePosScreen, viewContext);
                shape.InitialWorldPos = clickWorldPos;

                for (int i = 0; i < currentFrame.Ids.Count; i++)
                {
                    Vector3 entityPos = new Vector3(currentFrame.X[i], 0f, currentFrame.Z[i]);
                    if (Vector3.Distance(clickWorldPos, entityPos) < 2.0f)
                    {
                        shape.IsEntityTracked = true;
                        shape.TargetEntityId = currentFrame.Ids[i];
                        shape.OffsetFromEntity = clickWorldPos - entityPos;
                        break;
                    }
                }
            }
        }
        public void Undo()
        {
            if (undoManager.CanUndo())
            {
                var undoneState = undoManager.Undo();
                if (undoneState != null) pageManager.SetCurrentPageDrawables(undoneState);
            }
        }
        public void ProcessCanvasInteraction(
            Vector2 mousePosLogical, Vector2 mousePosScreen, Vector2 canvasOriginScreen, ImDrawListPtr drawList,
            bool isLMBDown, bool isLMBClicked, bool isLMBReleased, bool isLMBDoubleClicked,
            Func<DrawMode> getDrawModeFunc, Func<Vector4> getBrushColorFunc, Func<float> getBrushThicknessFunc,
            Func<bool> getShapeFilledFunc, ReplayRenderer.ViewContext? viewContext = null, float currentReplayTime = 0f,
            AetherBlackbox.Core.ReplayFrame? currentFrame = null)
        {
            var currentMode = getDrawModeFunc();
            Vector2 effectivePos = mousePosLogical;
            var currentDrawables = pageManager.GetCurrentPageDrawables();
            if (currentDrawables == null) return;

            if (isLMBDoubleClicked && currentMode == DrawMode.Select)
            {
                var hovered = getHoveredDrawableDelegate();
                if (hovered is DrawableText dt && !inPlaceTextEditor.IsCurrentlyEditing(dt))
                {
                    inPlaceTextEditor.BeginEdit(dt, canvasOriginScreen, ImGuiHelpers.GlobalScale);
                    return;
                }
            }

            bool allowInteraction = currentMode == DrawMode.Select || currentMode == DrawMode.Eraser;

            if (allowInteraction && currentMode == DrawMode.Select)
            {
                BaseDrawable? singleSelectedItem = selectedDrawablesRef.Count == 1 ? selectedDrawablesRef[0] : null;
                var hovered = getHoveredDrawableDelegate();

                shapeInteractionHandler.ProcessInteractions(
                    singleSelectedItem, selectedDrawablesRef, currentDrawables,
                    (mode) => 1, ref hovered,
                    effectivePos, mousePosScreen, canvasOriginScreen,
                    isLMBClicked, isLMBDown, isLMBReleased, drawList);

                setHoveredDrawableDelegate(hovered);
                return;
            }

            if (allowInteraction && currentMode == DrawMode.Eraser)
            {
                var hovered = getHoveredDrawableDelegate();
                shapeInteractionHandler.ProcessInteractions(
                    null, selectedDrawablesRef, currentDrawables,
                    (mode) => 1, ref hovered,
                    effectivePos, mousePosScreen, canvasOriginScreen,
                    false, false, false, drawList);
                setHoveredDrawableDelegate(hovered);

                if (isLMBClicked && hovered != null)
                {
                    undoManager.RecordAction(currentDrawables, "Eraser");
                    currentDrawables.Remove(hovered);
                    selectedDrawablesRef.Remove(hovered);
                    setHoveredDrawableDelegate(null);
                }
                return;
            }

            if (currentMode == DrawMode.TextTool)
            {
                if (isLMBClicked)
                {
                    undoManager.RecordAction(currentDrawables, "Add Text");
                    var newText = new DrawableText(effectivePos, "New Text", getBrushColorFunc(), 16f, 200f) { ReplayTime = currentReplayTime };
                    ApplyTrackingAndTiming(newText, currentReplayTime, effectivePos, mousePosScreen, viewContext, currentFrame);
                    currentDrawables.Add(newText);

                    foreach (var sel in selectedDrawablesRef) sel.IsSelected = false;
                    selectedDrawablesRef.Clear();
                    newText.IsSelected = true;
                    selectedDrawablesRef.Add(newText);
                    setHoveredDrawableDelegate(newText);

                    inPlaceTextEditor.BeginEdit(newText, canvasOriginScreen, ImGuiHelpers.GlobalScale);
                    setDrawModeAction(DrawMode.Select);
                }
                return;
            }

            if (IsImagePlacementMode(currentMode))
            {
                HandleImagePlacementInput(currentMode, effectivePos, mousePosScreen, isLMBClicked, currentDrawables, currentReplayTime, viewContext, currentFrame);
                return;
            }

            HandleShapeDrawingInput(currentMode, effectivePos, mousePosScreen, isLMBDown, isLMBClicked, isLMBReleased, currentReplayTime, viewContext, currentFrame);
        }


        private void HandleShapeDrawingInput(DrawMode currentMode, Vector2 effectivePos, Vector2 mousePosScreen, bool isLMBDown, bool isLMBClickedOnCanvas, bool isLMBReleased, float currentReplayTime, ReplayRenderer.ViewContext? viewContext, AetherBlackbox.Core.ReplayFrame? currentFrame)
        {
            if (isLMBDown)
            {
                if (!isDrawingOnCanvas && isLMBClickedOnCanvas)
                {
                    isDrawingOnCanvas = true;
                    foreach (var sel in selectedDrawablesRef) sel.IsSelected = false;
                    selectedDrawablesRef.Clear();
                    setHoveredDrawableDelegate(null);

                    if (currentMode == DrawMode.Laser)
                    {
                        var newLaser = new DrawableLaser(effectivePos, getBrushColorFunc(), getBrushThicknessFunc()) { ReplayTime = currentReplayTime };
                        ApplyTrackingAndTiming(newLaser, currentReplayTime, effectivePos, mousePosScreen, viewContext, currentFrame);
                        currentDrawingObjectInternal = newLaser;
                        _ephemeralDrawables.Add(newLaser);
                    }
                    else
                    {
                        currentDrawingObjectInternal = CreateNewDrawingObject(currentMode, effectivePos, getBrushColorFunc(), getBrushThicknessFunc(), getShapeFilledFunc != null && getShapeFilledFunc(), currentReplayTime);
                        if (currentDrawingObjectInternal != null)
                        {
                            undoManager.RecordAction(pageManager.GetCurrentPageDrawables(), $"Start Drawing {currentMode}");
                            currentDrawingObjectInternal.IsPreview = true;
                            ApplyTrackingAndTiming(currentDrawingObjectInternal, currentReplayTime, effectivePos, mousePosScreen, viewContext, currentFrame);
                        }
                    }
                }

                if (isDrawingOnCanvas && currentDrawingObjectInternal != null)
                {
                    if (currentDrawingObjectInternal is DrawableLaser laser)
                    {
                        laser.AddPoint(effectivePos);
                        if (pageManager.IsLiveMode && (DateTime.Now - laser.LastUpdateTime).TotalMilliseconds > 40)
                        {
                            var payload = new AetherBlackbox.Networking.NetworkPayload
                            {
                                PageIndex = pageManager.GetCurrentPageIndex(),
                                Action = AetherBlackbox.Networking.PayloadActionType.AddObjects,
                                Data = AetherBlackbox.Serialization.DrawableSerializer.SerializePageToBytes(new List<BaseDrawable> { laser })
                            };
                            _ = plugin.NetworkManager.SendStateUpdateAsync(payload);
                            laser.LastUpdateTime = DateTime.Now;
                        }
                    }
                    else if (currentDrawingObjectInternal is DrawablePath p) p.AddPoint(effectivePos);
                    else if (currentDrawingObjectInternal is DrawableDash d) d.AddPoint(effectivePos);
                    else currentDrawingObjectInternal.UpdatePreview(effectivePos);
                }
            }
            if (isDrawingOnCanvas && isLMBReleased)
            {
                FinalizeCurrentDrawing();
            }
        }
        private void FinalizeCurrentDrawing()
        {
            if (currentDrawingObjectInternal == null)
            {
                isDrawingOnCanvas = false;
                return;
            }

            if (currentDrawingObjectInternal is DrawableLaser laser)
            {
                _ = System.Threading.Tasks.Task.Run(async () =>
                {
                    await System.Threading.Tasks.Task.Delay(600);
                    if (pageManager.IsLiveMode)
                    {
                        using var ms = new System.IO.MemoryStream();
                        using var writer = new System.IO.BinaryWriter(ms);
                        writer.Write(1);
                        writer.Write(laser.UniqueId.ToByteArray());

                        var payload = new AetherBlackbox.Networking.NetworkPayload
                        {
                            PageIndex = pageManager.GetCurrentPageIndex(),
                            Action = AetherBlackbox.Networking.PayloadActionType.DeleteObjects,
                            Data = ms.ToArray()
                        };
                        _ = plugin.NetworkManager?.SendStateUpdateAsync(payload);
                    }
                });
                currentDrawingObjectInternal = null;
                isDrawingOnCanvas = false;
                return;
            }

            currentDrawingObjectInternal.IsPreview = false;
            var currentDrawables = pageManager.GetCurrentPageDrawables();

            if (currentDrawables != null)
            {
                bool isValidObject = true;
                if (currentDrawingObjectInternal is DrawablePath p && p.PointsRelative.Count < 2) isValidObject = false;
                else if (currentDrawingObjectInternal is DrawableDash d && d.PointsRelative.Count < 2) isValidObject = false;

                if (isValidObject)
                {
                    currentDrawables.Add(currentDrawingObjectInternal);

                    if (pageManager.IsLiveMode)
                    {
                        var payload = new AetherBlackbox.Networking.NetworkPayload
                        {
                            PageIndex = pageManager.GetCurrentPageIndex(),
                            Action = AetherBlackbox.Networking.PayloadActionType.AddObjects,
                            Data = AetherBlackbox.Serialization.DrawableSerializer.SerializePageToBytes(new List<BaseDrawable> { currentDrawingObjectInternal })
                        };
                        _ = plugin.NetworkManager?.SendStateUpdateAsync(payload);
                    }
                }
                else
                {
                    var undoneState = undoManager.Undo();
                    if (undoneState != null) pageManager.SetCurrentPageDrawables(undoneState);
                }
            }

            currentDrawingObjectInternal = null;
            isDrawingOnCanvas = false;
        }
        private bool IsImagePlacementMode(DrawMode mode)
        {
            return ToolRegistry.Tools.TryGetValue(mode, out var meta) && meta.IsPlaceableImage;
        }

        private void HandleImagePlacementInput(DrawMode currentMode, Vector2 mousePosLogical, Vector2 mousePosScreen, bool isLMBClickedOnCanvas, List<BaseDrawable> currentDrawablesOnPage, float currentReplayTime, ReplayRenderer.ViewContext? viewContext, AetherBlackbox.Core.ReplayFrame? currentFrame)
        {
            if (isLMBClickedOnCanvas && ToolRegistry.Tools.TryGetValue(currentMode, out var meta) && !string.IsNullOrEmpty(meta.CanvasImagePath))
            {
                var newImage = new DrawableImage(currentMode, meta.CanvasImagePath, mousePosLogical, meta.DefaultSize, Vector4.One) { ReplayTime = currentReplayTime };
                newImage.IsPreview = false;
                ApplyTrackingAndTiming(newImage, currentReplayTime, mousePosLogical, mousePosScreen, viewContext, currentFrame);
                undoManager.RecordAction(currentDrawablesOnPage, $"Place Image");
                currentDrawablesOnPage.Add(newImage);
            }
        }

        public static BaseDrawable? CreateNewDrawingObject(DrawMode mode, Vector2 startPosLogical, Vector4 color, float thickness, bool isFilled, float replayTime = 0f)
        {
            Vector4 finalColor = color;
            if (isFilled)
            {
                switch (mode)
                {
                    case DrawMode.Rectangle:
                    case DrawMode.Circle:
                    case DrawMode.Cone:
                    case DrawMode.Donut:
                    case DrawMode.Triangle:
                    case DrawMode.Arrow:
                    case DrawMode.Pie:
                        finalColor.W = 0.4f;
                        break;
                }
            }

            BaseDrawable? drawable = mode switch
            {
                DrawMode.Pen => new DrawablePath(startPosLogical, color, thickness),
                DrawMode.StraightLine => new DrawableStraightLine(startPosLogical, color, thickness),
                DrawMode.Dash => new DrawableDash(startPosLogical, color, thickness),
                DrawMode.Rectangle => new DrawableRectangle(startPosLogical, finalColor, thickness, isFilled),
                DrawMode.Circle => new DrawableCircle(startPosLogical, finalColor, thickness, isFilled),
                DrawMode.Donut => new DrawableDonut(startPosLogical, finalColor, thickness, isFilled),
                DrawMode.Starburst => new DrawableStarburst(startPosLogical, new Vector4(1f, 0.6f, 0f, 0.6f), thickness, true),
                DrawMode.Arrow => new DrawableArrow(startPosLogical, finalColor, thickness),
                DrawMode.Cone => new DrawableCone(startPosLogical, finalColor, thickness, isFilled),
                DrawMode.Triangle => new DrawableTriangle(startPosLogical, finalColor, thickness, isFilled),
                DrawMode.Pie => new DrawablePie(startPosLogical, finalColor, thickness, isFilled),
                _ => null,
            };

            if (drawable != null)
            {
                drawable.ReplayTime = replayTime;
            }

            return drawable;
        }
    }
}