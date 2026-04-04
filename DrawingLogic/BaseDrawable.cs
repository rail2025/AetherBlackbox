using System;
using System.Drawing;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using SixLabors.ImageSharp.Processing;

namespace AetherBlackbox.DrawingLogic
{
    public abstract class BaseDrawable
    {
        public Guid UniqueId { get; set; }
        public DrawMode ObjectDrawMode { get; set; } // Changed from protected set
        public Vector4 Color { get; set; }
        public float Thickness { get; set; }
        public bool IsFilled { get; set; }
        public bool IsLocked { get; set; } = false;
        public bool IsPreview { get; set; }
        public bool IsSelected { get; set; } = false;
        public bool IsHovered { get; set; } = false;

        public float ReplayTime { get; set; } = 0f;
        protected BaseDrawable()
        {
            this.UniqueId = Guid.NewGuid();
        }
        public string? Name { get; set; }

        public abstract void Draw(ImDrawListPtr drawList, Vector2 canvasOriginScreen);
        public virtual void DrawProjected(ImDrawListPtr drawList, ReplayRenderer.ViewContext viewContext)
        {
            Draw(drawList, viewContext.CanvasOrigin);
        }
        public abstract void DrawToImage(IImageProcessingContext context, Vector2 canvasOriginInOutputImage, float currentGlobalScale);
        public abstract RectangleF GetBoundingBox();
        public abstract bool IsHit(Vector2 queryPointOrEraserCenterRelative, float hitThresholdOrEraserRadius = 5.0f);
        public abstract BaseDrawable Clone();
        public abstract void Translate(Vector2 delta);

        public virtual void UpdatePreview(Vector2 currentPointRelative) { }

        protected void CopyBasePropertiesTo(BaseDrawable target)
        {
            // UniqueId is not copied; the clone gets its own new UniqueId upon its construction.
            target.ObjectDrawMode = this.ObjectDrawMode;
            target.Name = this.Name;
            target.Color = this.Color;
            target.Thickness = this.Thickness;
            target.IsFilled = this.IsFilled;
            target.IsLocked = this.IsLocked;
            target.IsPreview = false; // Cloned objects are generally not previews by default
            target.IsSelected = false;
            target.IsHovered = false;
            target.ReplayTime = this.ReplayTime;
        }
    }
}
