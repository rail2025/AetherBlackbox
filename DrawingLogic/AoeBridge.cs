using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AetherBlackbox.Core;
using AetherBlackbox.Core.Mechanics;

namespace AetherBlackbox.DrawingLogic
{
    public class AoeBridge
    {
        public void SyncActiveAoEs(List<ActiveAoe> activeAoEs, PageManager pageManager)
        {
            var drawables = pageManager.GetCurrentPageDrawables();
            if (drawables == null) return;

            var activeIds = new HashSet<string>(activeAoEs.Select(a => a.StableId));
            drawables.RemoveAll(d => d.AutoId != null && !d.IsUserEdited && !activeIds.Contains(d.AutoId));

            float scale = 8f; // DefaultPixelsPerYard

            foreach (var aoe in activeAoEs)
            {
                var existing = drawables.FirstOrDefault(d => d.AutoId == aoe.StableId);

                if (existing != null)
                {
                    if (!existing.IsUserEdited)
                    {
                        existing.EndTime = aoe.ExpirationTime;
                    }
                    continue;
                }

                BaseDrawable? newObject = null;
                Vector2 centerRel = Vector2.Zero;
                float r = aoe.Template.Radius * scale;
                float w = aoe.Template.Width * scale;
                float ir = aoe.Template.InnerRadius * scale;
                float t = aoe.Template.Thickness > 0 ? aoe.Template.Thickness : 2f;
                bool f = aoe.Template.IsFilled;
                var c = aoe.Template.Color;

                Vector3 initialWorldPos = aoe.Origin;

                if (aoe.Template.Shape == AoeShape.Circle)
                {
                    newObject = new DrawableCircle(centerRel, c, t, f) { Radius = r };
                }
                else if (aoe.Template.Shape == AoeShape.Donut)
                {
                    newObject = new DrawableDonut(centerRel, c, t, f, r, ir);
                }
                else if (aoe.Template.Shape == AoeShape.Rect)
                {
                    var start = new Vector2(-w / 2f, -r / 2f);
                    newObject = new DrawableRectangle(start, c, t, f)
                    {
                        EndPointRelative = new Vector2(w / 2f, r / 2f), // Radius corresponds to length
                        RotationAngle = -aoe.Rotation
                    };

                    float lengthYalms = aoe.Template.Radius;
                    float cx = aoe.Origin.X + (lengthYalms / 2f) * (float)Math.Sin(aoe.Rotation);
                    float cz = aoe.Origin.Z + (lengthYalms / 2f) * (float)Math.Cos(aoe.Rotation);
                    initialWorldPos = new Vector3(cx, aoe.Origin.Y, cz);
                }
                else if (aoe.Template.Shape == AoeShape.Cone || aoe.Template.Shape == AoeShape.Pie)
                {
                    newObject = new DrawablePie(centerRel, c, t, f)
                    {
                        Radius = r,
                        RotationAngle = -aoe.Rotation - ((aoe.Template.Angle * (MathF.PI / 180f)) / 2f) + (MathF.PI / 2f),
                        SweepAngle = aoe.Template.Angle * (MathF.PI / 180f)
                    };
                }

                if (newObject != null)
                {
                    newObject.Name = aoe.Template.Name ?? "Auto-AOE";

                    newObject.AutoId = aoe.StableId;
                    newObject.IsUserEdited = false;

                    newObject.InitialWorldPos = initialWorldPos;
                    newObject.IsEntityTracked = false;
                    newObject.TargetEntityId = 0;
                    newObject.OffsetFromEntity = Vector3.Zero;

                    newObject.StartTime = aoe.ExpirationTime - aoe.Template.Duration;
                    newObject.EndTime = aoe.ExpirationTime;
                    newObject.IsPreview = false;

                   /* newObject.IsEntityTracked = true;
                    newObject.TargetEntityId = aoe.SourceEntityId;
                    newObject.OffsetFromEntity = aoe.OffsetFromEntity; */
                    newObject.InitialLogicalPos = Vector2.Zero;

                    drawables.Add(newObject);
                }
            }
        }
    }
}