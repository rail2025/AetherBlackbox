using System.Collections.Generic;
using System.Numerics;
using static AetherBlackbox.Core.Mechanics.AoeShape;

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
        public Vector4 Color { get; set; } = new Vector4(1f, 0.5f, 0f, 0.3f); // change to color for aoes. this is orange
    }

    public static class UMAD
    {
        public static AoeInfo Circle(float radius) => new() { Shape = AoeShape.Circle, Radius = radius };
        public static AoeInfo Circle(float radius, Vector4 color) => new() { Shape = AoeShape.Circle, Radius = radius, Color = color };
        public static AoeInfo Donut(float outer, float inner) => new() { Shape = AoeShape.Donut, Radius = outer, InnerRadius = inner };
        public static AoeInfo Donut(float outer, float inner, Vector4 color) => new() { Shape = AoeShape.Donut, Radius = outer, InnerRadius = inner, Color = color };

        public static AoeInfo Rect(float length, float width) => new() { Shape = AoeShape.Rect, Radius = length, Width = width };
        public static AoeInfo Rect(float length, float width, Vector4 color) => new() { Shape = AoeShape.Rect, Radius = length, Width = width, Color = color };

        public static AoeInfo Cone(float radius, float angle) => new() { Shape = AoeShape.Cone, Radius = radius, Angle = angle };
        public static AoeInfo Cone(float radius, float angle, Vector4 color) => new() { Shape = AoeShape.Cone, Radius = radius, Angle = angle, Color = color };

        /*
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

        public static readonly Dictionary<uint, AoeInfo> Abilities = new()
        {
            // P1
            { 50179, Cone(100f, 120f) },  // RevoltingRuinIIIFirst
            { 50401, Cone(100f, 120f) },  // RevoltingRuinIIISecond
            { 47768, Cone(40f, 90f) },    // BlizzardIIIBlowout1
            { 47771, Cone(40f, 90f) },    // BlizzardIIIBlowoutFake
            { 47774, Cone(40f, 90f) },    // BlizzardIIIBlowout2
            { 47775, Rect(40f, 10f) },    // ThrummingThunderIII1
            { 47776, Rect(40f, 10f) },    // ThrummingThunderIIIFake
            { 47777, Rect(40f, 10f) },    // ThrummingThunderIII2
            { 47778, Circle(5f) },        // FlagrantFireIIISpread
            { 47779, Circle(6f) },        // FlagrantFireIIIStack
            { 47783, Circle(6f) },        // DoubleTroubleTrapStack
            { 47784, Rect(100f, 6f) },    // WaveCannon
            { 47786, Circle(4f) },        // ExplosionP1
            { 47787, Circle(100f) },      // UnmitigatedExplosionP1
            { 50722, Circle(100f) },      // LightOfJudgmentP1
            { 49739, Circle(5f) },        // Hyperdrive
            { 47788, Circle(5f) },        // Gravitas
            { 47789, Circle(100f) },      // GravitationalExplosion
            { 47792, Circle(5f) },        // Vitrophyre
            { 47793, Cone(100f, 180f) },  // GravitationalWave
            { 47794, Cone(100f, 180f) },  // IntemperateWill
            { 47791, Circle(5f) },        // GravityIII
            { 47802, Circle(2f) },        // TeleTrouncingArrowSpawn
            { 47798, Circle(5f) },        // IdyllicWill
            { 47795, Circle(100f) },      // AveMaria
            { 47796, Circle(100f) },      // IndolentWill
            { 47803, Circle(100f) },      // LightOfJudgmentP1Enrage

            // P2
            { 49740, Circle(5f) },        // UltimateEmbrace
            { 47804, Circle(100f) },      // Forsaken
            { 47806, Circle(4f) },        // ThePathOfLight
            { 47807, Circle(100f) },      // TheRiverOfLight
            { 47808, Circle(5f) },        // Spelldriver
            { 47809, Circle(5f) },        // Spellscatter
            { 47810, Cone(40f, 90f) },    // Spellwave
            { 47830, Circle(5f) },        // FuturesEndBossAOE
            { 47831, Circle(5f) },        // PastsEndBossAOE
            { 47832, Circle(5f) },        // FuturesEndCloneAOE
            { 47833, Circle(5f) },        // PastsEndCloneAOE
            { 47836, Cone(100f, 180f) },  // AllThingsEnding1
            { 47837, Cone(100f, 180f) },  // AllThingsEnding2
            { 47805, Circle(100f) },      // LightOfJudgmentP2
            { 47840, Circle(6f) },        // Trine
            { 47821, Rect(80f, 40f) },    // WingsOfDestructionL
            { 47822, Rect(80f, 40f) },    // WingsOfDestructionR
            { 47823, Circle(7f) },        // WingsOfDestructionBuster
            { 47841, Circle(100f) },      // LightOfJudgmentP2Enrage

            // P3
            { 50167, Circle(40f) },       // AeroIIIAssault
            { 47858, Circle(100f) },      // BowelsOfAgony
            { 47890, Circle(11f) },       // ThunderIIICircle
            { 47859, Circle(5f) },        // StrayFlames
            { 47860, Donut(10f, 4f) },    // Inferno
            { 47862, Donut(10f, 4f) },    // StraySpray
            { 47861, Circle(5f) },        // Tsunami
            { 47864, Circle(6f) },        // Cyclone
            { 47884, Circle(5f) },        // ThunderIIIBuster
            { 47871, Cone(40f, 90f) },    // LatLongShockwave
            { 47843, Circle(100f) },      // UltimaBlasterRaidwide
            { 47872, Circle(100f) },      // UmbraSmash
            { 47891, Circle(100f) },      // VacuumWave
            { 47844, Rect(100f, 6f) },    // UltimaBlasterCharge
            { 50546, Circle(100f) },      // EarthquakeRaidwide
            { 47866, Circle(100f) },      // EarthquakeInstant
            { 47848, Circle(13f) },       // SlapHappyBig
            { 47849, Circle(6f) },        // SlapHappySmall
            { 47850, Cone(100f, 45f) },   // SlapHappyShockingImpact
            { 47851, Cone(100f, 45f) },   // SlapHappyShockwave
            { 47868, Rect(125f, 6f) },    // Nothingness
            { 47873, Rect(60f, 80f) },    // DamningEdict
            { 47854, Rect(100f, 16f) }    // LookUponMeAndDespair
        };
    }
}