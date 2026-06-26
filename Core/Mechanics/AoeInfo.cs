using System;
using System.Collections.Generic;
using System.Text;
using System.Numerics;

namespace AetherBlackbox.Core.Mechanics
{
    public enum AoeShape
    {
        Circle,
        Cone,
        Rect,
        Donut
    }

    public class AoeInfo
    {
        public AoeShape Shape { get; set; }
        public float Radius { get; set; }
        public float Width { get; set; }
        public float InnerRadius { get; set; }
        public float Angle { get; set; }
        public Vector4 Color { get; set; } = new Vector4(1f, 0.5f, 0f, 0.3f);
        public float Thickness { get; set; } = 2f;
        public bool IsFilled { get; set; } = true;
        public float Duration { get; set; } = 0.5f;
    }
}