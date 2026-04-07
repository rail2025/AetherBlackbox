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
        public float StartTime { get; set; } = 0f;
        public float EndTime { get; set; } = float.MaxValue;
        public bool IsEntityTracked { get; set; } = false;
        public uint TargetEntityId { get; set; } = 0;
        public Vector3 OffsetFromEntity { get; set; } = Vector3.Zero;
        public Vector3 InitialWorldPos { get; set; } = Vector3.Zero;
        public Vector2 InitialLogicalPos { get; set; } = Vector2.Zero;
        protected BaseDrawable()
        {
            this.UniqueId = Guid.NewGuid();
        }
        public string? Name { get; set; }

        public abstract void Draw(ImDrawListPtr drawList, Vector2 canvasOriginScreen);
        public virtual void DrawProjected(ImDrawListPtr drawList, ReplayRenderer.ViewContext viewContext, AetherBlackbox.Core.ReplayFrame? currentFrame, float currentReplayTime)
        {
            if (!IsPreview && (currentReplayTime < StartTime || currentReplayTime > EndTime))
                return;

            Vector2 origin = viewContext.CanvasOrigin;

            if (this.IsEntityTracked && currentFrame != null)
            {
                int index = currentFrame.Ids.IndexOf(this.TargetEntityId);
                if (index != -1)
                {
                    Vector3 entityPos = new Vector3(currentFrame.X[index], 0f, currentFrame.Z[index]);
                    Vector3 currentWorldPos = entityPos + this.OffsetFromEntity;
                    Vector2 targetScreen = ReplayRenderer.WorldToScreen(currentWorldPos, viewContext);
                    Vector2 initialScreenOffset = this.InitialLogicalPos * Dalamud.Interface.Utility.ImGuiHelpers.GlobalScale;
                    origin = targetScreen - initialScreenOffset;
                }
                else return;
            }
            else
            {
                origin += viewContext.PanOffset;
            }

            Draw(drawList, origin);
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

            target.StartTime = this.StartTime;
            target.EndTime = this.EndTime;
            target.IsEntityTracked = this.IsEntityTracked;
            target.TargetEntityId = this.TargetEntityId;
            target.OffsetFromEntity = this.OffsetFromEntity;
            target.InitialWorldPos = this.InitialWorldPos;
            target.InitialLogicalPos = this.InitialLogicalPos;
        }
    }
}
