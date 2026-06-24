using System.Collections.Generic;
using System.Numerics;
using static AetherBlackbox.Core.Mechanics.AoeShape;

namespace AetherBlackbox.Core.Mechanics
{
    public static class _FightTemplate
    {
        // Shape Shortcuts
        public static AoeInfo Circle(float radius) => new() { Shape = AoeShape.Circle, Radius = radius };
        public static AoeInfo Circle(float radius, Vector4 color) => new() { Shape = AoeShape.Circle, Radius = radius, Color = color };

        public static AoeInfo Donut(float outer, float inner) => new() { Shape = AoeShape.Donut, Radius = outer, InnerRadius = inner };
        public static AoeInfo Donut(float outer, float inner, Vector4 color) => new() { Shape = AoeShape.Donut, Radius = outer, InnerRadius = inner, Color = color };

        public static AoeInfo Rect(float length, float width) => new() { Shape = AoeShape.Rect, Radius = length, Width = width };
        public static AoeInfo Rect(float length, float width, Vector4 color) => new() { Shape = AoeShape.Rect, Radius = length, Width = width, Color = color };

        public static AoeInfo Cone(float radius, float angle) => new() { Shape = AoeShape.Cone, Radius = radius, Angle = angle };
        public static AoeInfo Cone(float radius, float angle, Vector4 color) => new() { Shape = AoeShape.Cone, Radius = radius, Angle = angle, Color = color };

        // Ability Database
        public static readonly Dictionary<uint, AoeInfo> Abilities = new()
        {
            /* * HOW TO DETECT AND ADD NEW AOES
             * 1. Find Action ID via logger.
             * 2. Measure in-game shape (Radius, Angle, Width).
             * 3. Add entry below using the format:
             *
             * ============================================================================
             * TO ADD NEW ABILITIES
             * Format: { [Spell ID], [ShapeShortcut]([Parameters]) }, // [Spell Name]
             * * SHAPE SHORTCUTS & REQUIRED PARAMETERS:
             * * 1. Circle: Circle(Radius)
             * Example: { 12345, Circle(5f) }, // Basic 5y circle
             
             * * 2. Cone:   Cone(Radius, Angle)
             * Example: { 12346, Cone(40f, 90f) }, // 40y long, 90-degree wide cone
             
             * * 3. Rect:   Rect(Length, Width)
             * Example: { 12347, Rect(40f, 10f) }, // 40y long, 10y wide line attack
             
             * * 4. Donut:  Donut(OuterRadius, InnerRadius)
             * Example: { 12348, Donut(10f, 4f) }, // 10y outer edge, 4y safe hole inside
             
             * * CUSTOM COLORS (Optional):
             * Add a Vector4(Red, Green, Blue, Transparency) to any shape from 0f to 1f.
             * Example: { 12349, Circle(5f, new Vector4(0f, 1f, 0f, 0.5f)) }, // Green circle
             * ============================================================================
             */

            // Add abilities here
        };
    }
}