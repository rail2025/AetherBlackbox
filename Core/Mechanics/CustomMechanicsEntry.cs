using System;
using System.Collections.Generic;
using System.Text;
using System.Numerics;

namespace AetherBlackbox.Core.Mechanics
{
    public class CustomMechanicEntry
    {
        public int Version { get; set; } = 1;
        public string Name { get; set; } = string.Empty;
        public uint ActionId { get; set; }
        public uint ZoneId { get; set; }
        public uint SourceActorId { get; set; }
        public uint TerritoryId { get; set; }
        public MechanicSourceType SourceType { get; set; }
        public List<string> Tags { get; set; } = new();
        public string OriginFile { get; set; } = string.Empty;

        public AoeShape Shape { get; set; }
        public float Radius { get; set; }
        public float Width { get; set; }
        public float InnerRadius { get; set; }
        public float Angle { get; set; }
        public Vector4 Color { get; set; } = new(1f, 0.5f, 0f, 0.4f);
        public float Thickness { get; set; }
        public bool IsFilled { get; set; } = true;
        public float Duration { get; set; }
    }
}
