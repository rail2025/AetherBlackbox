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
        private DrawableLaser? activeLaser;
        private DateTime lastLaserUpdate = DateTime.MinValue;

        public BaseDrawable? GetCurrentDrawingObjectForPreview() => activeLaser;
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
            Func<bool> getShapeFilledFunc, ReplayRenderer.ViewContext? viewContext = null, float currentReplayTime = 0f)
        {
            var currentMode = getDrawModeFunc();
            Vector2 effectivePos = mousePosLogical;
            var currentDrawables = pageManager.GetCurrentPageDrawables();

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

            if (currentMode == DrawMode.Laser)
            {
                if (isLMBClicked)
                {
                    var color = getBrushColorFunc();
                    var thickness = getBrushThicknessFunc();
                    activeLaser = new DrawableLaser(effectivePos, color, thickness) { ReplayTime = currentReplayTime };
                    lastLaserUpdate = DateTime.Now;
                }
                else if (isLMBDown && activeLaser != null)
                {
                    activeLaser.AddPoint(effectivePos);

                    if ((DateTime.Now - lastLaserUpdate).TotalMilliseconds > 40)
                    {
                        var points = activeLaser.GetPoints();
                        int byteCount = 16 + 4 + (points.Count * 8);
                        using var ms = new System.IO.MemoryStream(byteCount);
                        using var writer = new System.IO.BinaryWriter(ms);

                        writer.Write(0.718f);
                        writer.Write(0.973f);
                        writer.Write(0.718f);
                        writer.Write(1.0f);

                        writer.Write(points.Count);
                        foreach (var p in points)
                        {
                            writer.Write(p.X);
                            writer.Write(p.Y);
                        }

                        plugin.NetworkManager?.SendStateUpdateAsync(new Networking.NetworkPayload
                        {
                            PageIndex = 0,
                            Action = (Networking.PayloadActionType)7,
                            Data = ms.ToArray()
                        });
                        lastLaserUpdate = DateTime.Now;
                    }
                }
                else if (isLMBReleased && activeLaser != null)
                {
                    activeLaser = null;
                }
                return;
            }

            if (currentMode == DrawMode.TextTool)
            {
                if (isLMBClicked)
                {
                    var newText = new DrawableText(effectivePos, "New Text", getBrushColorFunc(), 16f, 200f) { ReplayTime = currentReplayTime };
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
                HandleImagePlacementInput(currentMode, effectivePos, isLMBClicked, currentDrawables, currentReplayTime);
                return;
            }

            if (isLMBClicked)
            {
                var color = getBrushColorFunc();
                var thickness = getBrushThicknessFunc();
                bool isFilled = getShapeFilledFunc != null && getShapeFilledFunc();
                var newShape = CreateNewDrawingObject(currentMode, effectivePos, color, thickness, isFilled, currentReplayTime);
                if (newShape != null)
                {
                    newShape.IsPreview = true;
                    currentDrawables.Add(newShape);
                }
            }
            else if (isLMBDown)
            {
                var previewShape = currentDrawables.LastOrDefault(d => d.IsPreview);
                if (previewShape != null)
                {
                    if (previewShape is DrawablePath p) p.AddPoint(effectivePos);
                    else if (previewShape is DrawableDash d) d.AddPoint(effectivePos);
                    else previewShape.UpdatePreview(effectivePos);
                }
            }
            else if (isLMBReleased)
            {
                var previewShape = currentDrawables.LastOrDefault(d => d.IsPreview);
                if (previewShape != null)
                {
                    previewShape.IsPreview = false;
                    if (previewShape is DrawablePath p && p.PointsRelative.Count < 2) currentDrawables.Remove(previewShape);
                    else if (previewShape is DrawableDash d && d.PointsRelative.Count < 2) currentDrawables.Remove(previewShape);
                }
            }
        }

        private bool IsImagePlacementMode(DrawMode mode)
        {
            return mode switch
            {
                DrawMode.BossImage or DrawMode.CircleAoEImage or DrawMode.DonutAoEImage or DrawMode.FlareImage or
                DrawMode.LineStackImage or DrawMode.SpreadImage or DrawMode.StackImage or DrawMode.GazeImage or DrawMode.TowerImage or DrawMode.ExasImage or
                DrawMode.Image => true,
                _ => false,
            };
        }

        private void HandleImagePlacementInput(DrawMode currentMode, Vector2 mousePosLogical, bool isLMBClickedOnCanvas, List<BaseDrawable> currentDrawablesOnPage, float currentReplayTime)
        {
            if (isLMBClickedOnCanvas)
            {
                string imagePath = "";
                Vector2 imageUnscaledSize = new Vector2(30f, 30f);
                Vector4 imageTint = Vector4.One;
                switch (currentMode)
                {
                    case DrawMode.BossImage: imagePath = "PluginImages.svg.boss.svg"; imageUnscaledSize = new Vector2(60f, 60f); break;
                    case DrawMode.CircleAoEImage: imagePath = "PluginImages.svg.prox_aoe.svg"; imageUnscaledSize = new Vector2(80f, 80f); break;
                    case DrawMode.DonutAoEImage: imagePath = "PluginImages.svg.donut.svg"; imageUnscaledSize = new Vector2(100f, 100f); break;
                    case DrawMode.FlareImage: imagePath = "PluginImages.svg.flare.svg"; imageUnscaledSize = new Vector2(60f, 60f); break;
                    case DrawMode.LineStackImage: imagePath = "PluginImages.svg.line_stack.svg"; imageUnscaledSize = new Vector2(30f, 60f); break;
                    case DrawMode.SpreadImage: imagePath = "PluginImages.svg.spread.svg"; imageUnscaledSize = new Vector2(60f, 60f); break;
                    case DrawMode.StackImage: imagePath = "PluginImages.svg.stack.svg"; imageUnscaledSize = new Vector2(60f, 60f); break;
                    case DrawMode.GazeImage: imagePath = "PluginImages.svg.gaze.png"; imageUnscaledSize = new Vector2(80f, 80f); break;
                    case DrawMode.TowerImage: imagePath = "PluginImages.svg.tower.png"; imageUnscaledSize = new Vector2(80f, 80f); break;
                    case DrawMode.ExasImage: imagePath = "PluginImages.svg.exas.svg"; imageUnscaledSize = new Vector2(80f, 80f); break;
                    case DrawMode.Image: imagePath = "PluginImages.toolbar.Square.png"; imageUnscaledSize = new Vector2(25f, 25f); break;
                }

                if (!string.IsNullOrEmpty(imagePath))
                {
                    var newImage = new DrawableImage(currentMode, imagePath, mousePosLogical, imageUnscaledSize, imageTint) { ReplayTime = currentReplayTime };
                    newImage.IsPreview = false;
                    undoManager.RecordAction(currentDrawablesOnPage, $"Place Image");
                    currentDrawablesOnPage.Add(newImage);
                }
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