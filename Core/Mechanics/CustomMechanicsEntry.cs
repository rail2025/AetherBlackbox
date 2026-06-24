using System;
using System.Collections.Generic;
using System.Text;
using System.Numerics;

namespace AetherBlackbox.Core.Mechanics
{
    public class CustomMechanicEntry
    {
        public string Name { get; set; } = string.Empty;
        public uint ActionId { get; set; }
        public MechanicSourceType SourceType { get; set; }

        public AoeShape Shape { get; set; }
        public float Radius { get; set; }
        public float Width { get; set; }
        public float InnerRadius { get; set; }
        public float Angle { get; set; }
        public Vector4 Color { get; set; }
        public float Thickness { get; set; }
        public bool IsFilled { get; set; }
        public float Duration { get; set; }
    }
}
