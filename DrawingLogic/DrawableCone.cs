using System;
using System.Drawing;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Processing;

namespace AetherBlackbox.DrawingLogic
{
    public class DrawableCone : BaseDrawable
    {
        public Vector2 ApexRelative { get; set; }
        public Vector2 BaseCenterRelative { get; set; }
        public float RotationAngle { get; set; } = 0f;
        public static readonly float ConeWidthFactor = 0.3f;

        public DrawableCone(Vector2 apexRelative, Vector4 color, float unscaledThickness, bool isFilled)
        {
            this.ObjectDrawMode = DrawMode.Cone;
            this.ApexRelative = apexRelative;
            this.BaseCenterRelative = apexRelative;
            this.Color = color;
            this.Thickness = unscaledThickness;
            this.IsFilled = isFilled;
            this.IsPreview = true;
        }

        public override void UpdatePreview(Vector2 newBaseCenterRelativeWhileDrawing)
        {
            this.BaseCenterRelative = newBaseCenterRelativeWhileDrawing;
        }
        public override void Draw(ImDrawListPtr drawList, Vector2 canvasOriginScreen)
        {
            var displayColorVec = IsSelected ? new Vector4(1, 1, 0, 1) : (IsHovered ? new Vector4(0, 1, 1, 1) : Color);
            uint displayColor = ImGui.GetColorU32(displayColorVec);
            float scaledThickness = Math.Max(1f, Thickness * ImGuiHelpers.GlobalScale);
            if (IsSelected || IsHovered) scaledThickness += 2f * ImGuiHelpers.GlobalScale;

            var (localApex, localBaseVert1, localBaseVert2) = GetLocalUnrotatedVertices();

            // If the cone is too small, just draw a dot at its apex.
            if (localBaseVert1 == Vector2.Zero && localBaseVert2 == Vector2.Zero && IsPreview)
            {
                Vector2 screenApexForDot = (this.ApexRelative * ImGuiHelpers.GlobalScale) + canvasOriginScreen;
                drawList.AddCircleFilled(screenApexForDot, scaledThickness / 2f + (1f * ImGuiHelpers.GlobalScale), displayColor);
                return;
            }

            Matrix3x2 transform =
                Matrix3x2.CreateRotation(this.RotationAngle) * Matrix3x2.CreateTranslation(this.ApexRelative) * Matrix3x2.CreateScale(ImGuiHelpers.GlobalScale) *
                Matrix3x2.CreateTranslation(canvasOriginScreen);

            Vector2 screenApex = Vector2.Transform(localApex, transform);
            Vector2 screenB1 = Vector2.Transform(localBaseVert1, transform);
            Vector2 screenB2 = Vector2.Transform(localBaseVert2, transform);

            if (IsFilled)
            {
                drawList.AddTriangleFilled(screenApex, screenB1, screenB2, displayColor);
            }
            else
            {
                drawList.AddLine(screenApex, screenB1, displayColor, scaledThickness);
                drawList.AddLine(screenApex, screenB2, displayColor, scaledThickness);
                drawList.AddLine(screenB1, screenB2, displayColor, scaledThickness);
            }
        }

        public override void DrawToImage(IImageProcessingContext context, Vector2 canvasOriginInOutputImage, float currentGlobalScale)
        {
            var imageSharpColor = SixLabors.ImageSharp.Color.FromRgba(
                (byte)(Color.X * 255), (byte)(Color.Y * 255),
                (byte)(Color.Z * 255), (byte)(Color.W * 255)
            );
            float scaledThickness = Math.Max(1f, this.Thickness * currentGlobalScale);

            var (localApex, localBaseVert1, localBaseVert2) = GetLocalUnrotatedVertices();

            if (localBaseVert1 == Vector2.Zero && localBaseVert2 == Vector2.Zero) return;

            Matrix3x2 transformMatrix =
                Matrix3x2.CreateRotation(this.RotationAngle) * Matrix3x2.CreateTranslation(this.ApexRelative) * Matrix3x2.CreateScale(currentGlobalScale) * Matrix3x2.CreateTranslation(canvasOriginInOutputImage);

            var screenApex = Vector2.Transform(localApex, transformMatrix);
            var screenB1 = Vector2.Transform(localBaseVert1, transformMatrix);
            var screenB2 = Vector2.Transform(localBaseVert2, transformMatrix);

            var path = new PathBuilder()
                .AddLine(new SixLabors.ImageSharp.PointF(screenApex.X, screenApex.Y), new SixLabors.ImageSharp.PointF(screenB1.X, screenB1.Y))
                .AddLine(new SixLabors.ImageSharp.PointF(screenB1.X, screenB1.Y), new SixLabors.ImageSharp.PointF(screenB2.X, screenB2.Y))
                .CloseFigure()
                .Build();

            if (IsFilled)
            {
                context.Fill(imageSharpColor, path);
            }
            else
            {
                context.Draw(Pens.Solid(imageSharpColor, scaledThickness), path);
            }
        }

        public override System.Drawing.RectangleF GetBoundingBox()
        {
            var (localApex, localB1, localB2) = GetLocalUnrotatedVertices();

            // Create the transform for rotation and position, but not for scaling.
            Matrix3x2 transform = Matrix3x2.CreateRotation(this.RotationAngle) * Matrix3x2.CreateTranslation(this.ApexRelative);

            var v1 = Vector2.Transform(localApex, transform);
            var v2 = Vector2.Transform(localB1, transform);
            var v3 = Vector2.Transform(localB2, transform);

            float minX = Math.Min(v1.X, Math.Min(v2.X, v3.X));
            float minY = Math.Min(v1.Y, Math.Min(v2.Y, v3.Y));
            float maxX = Math.Max(v1.X, Math.Max(v2.X, v3.X));
            float maxY = Math.Max(v1.Y, Math.Max(v2.Y, v3.Y));

            return new System.Drawing.RectangleF(minX, minY, maxX - minX, maxY - minY);
        }

        public override bool IsHit(Vector2 queryPointCanvasRelative, float unscaledHitThreshold = 5.0f)
        {
            // Reject degenerate cones
            if ((this.BaseCenterRelative - this.ApexRelative).LengthSquared() < 0.01f) return false;
            // Transform the query point into the cone's local, unrotated coordinate space.
            Vector2 localQueryPoint = Vector2.Transform(queryPointCanvasRelative - this.ApexRelative, Matrix3x2.CreateRotation(-this.RotationAngle));

            var (localApex, localB1, localB2) = GetLocalUnrotatedVertices();

            if ((this.BaseCenterRelative - this.ApexRelative).LengthSquared() < 0.01f)
                return Vector2.DistanceSquared(localQueryPoint, localApex) < (unscaledHitThreshold * unscaledHitThreshold);

            if (IsFilled)
            {
                return HitDetection.PointInTriangle(localQueryPoint, localApex, localB1, localB2);
            }
            else
            {
                float effectiveDist = unscaledHitThreshold + this.Thickness / 2f;
                if (HitDetection.DistancePointToLineSegment(localQueryPoint, localApex, localB1) <= effectiveDist) return true;
                if (HitDetection.DistancePointToLineSegment(localQueryPoint, localApex, localB2) <= effectiveDist) return true;
                if (HitDetection.DistancePointToLineSegment(localQueryPoint, localB1, localB2) <= effectiveDist) return true;
            }
            return false;
        }

        public override BaseDrawable Clone()
        {
            var newCone = new DrawableCone(this.ApexRelative, this.Color, this.Thickness, this.IsFilled)
            {
                BaseCenterRelative = this.BaseCenterRelative,
                RotationAngle = this.RotationAngle
            };
            CopyBasePropertiesTo(newCone);
            return newCone;
        }

        public override void Translate(Vector2 delta)
        {
            this.ApexRelative += delta;
            this.BaseCenterRelative += delta;
        }

        public void SetApex(Vector2 newApexLogical)
        {
            Vector2 diff = newApexLogical - this.ApexRelative;
            this.ApexRelative = newApexLogical;
            this.BaseCenterRelative += diff;
        }

        public void SetBaseCenter(Vector2 newBaseCenterLogical)
        {
            this.BaseCenterRelative = newBaseCenterLogical;
        }

        private (Vector2 localApex, Vector2 localBaseVert1, Vector2 localBaseVert2) GetLocalUnrotatedVertices()
        {
            Vector2 localBaseCenterFromApexVec = this.BaseCenterRelative - this.ApexRelative;
            float height = localBaseCenterFromApexVec.Length();

            // If height is negligible, the cone is a point.
            if (height < 0.1f)
                return (Vector2.Zero, Vector2.Zero, Vector2.Zero);

            Vector2 direction = Vector2.Normalize(localBaseCenterFromApexVec);
            float baseHalfWidth = height * ConeWidthFactor;

            // Calculate the two vertices at the base of the cone.
            Vector2 perpendicular = new Vector2(direction.Y, -direction.X);
            Vector2 localBaseVert1 = localBaseCenterFromApexVec + perpendicular * baseHalfWidth;
            Vector2 localBaseVert2 = localBaseCenterFromApexVec - perpendicular * baseHalfWidth;

            return (Vector2.Zero, localBaseVert1, localBaseVert2);
        }
    }
}
