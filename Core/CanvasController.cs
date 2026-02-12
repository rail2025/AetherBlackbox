using System;
using System.Collections.Generic;
using System.Numerics;
using AetherBlackbox.DrawingLogic;
using Dalamud.Interface.Utility;
using Dalamud.Bindings.ImGui;

namespace AetherBlackbox.Core
{
    public class CanvasController
    {
        private readonly PageManager pageManager;
        private readonly Plugin plugin;
        private readonly Configuration configuration;

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
        }

        public BaseDrawable? GetCurrentDrawingObjectForPreview() => null;

        public void ProcessCanvasInteraction(
            Vector2 mousePosLogical, Vector2 mousePosScreen, Vector2 canvasOriginScreen, ImDrawListPtr drawList,
            bool isLMBDown, bool isLMBClicked, bool isLMBReleased, bool isLMBDoubleClicked,
            Func<DrawMode, int> getLayerPriorityFunc)
        {
            
        }

        public static BaseDrawable? CreateNewDrawingObject(DrawMode mode, Vector2 startPosLogical, Vector4 color, float thickness, bool isFilled)
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

            return mode switch
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
        }
    }
}