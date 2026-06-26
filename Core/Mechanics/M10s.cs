using System.Collections.Generic;
using System.Numerics;
using static AetherBlackbox.Core.Mechanics.AoeShape;

namespace AetherBlackbox.Core.Mechanics
{
    public static class M10s
    {
        public static AoeInfo Circle(float radius) => new() { Shape = AoeShape.Circle, Radius = radius };
        public static AoeInfo Circle(float radius, Vector4 color) => new() { Shape = AoeShape.Circle, Radius = radius, Color = color };
        public static AoeInfo Donut(float outer, float inner) => new() { Shape = AoeShape.Donut, Radius = outer, InnerRadius = inner };
        public static AoeInfo Donut(float outer, float inner, Vector4 color) => new() { Shape = AoeShape.Donut, Radius = outer, InnerRadius = inner, Color = color };

        public static AoeInfo Rect(float length, float width) => new() { Shape = AoeShape.Rect, Radius = length, Width = width };
        public static AoeInfo Rect(float length, float width, Vector4 color) => new() { Shape = AoeShape.Rect, Radius = length, Width = width, Color = color };

        public static AoeInfo Cone(float radius, float angle) => new() { Shape = AoeShape.Cone, Radius = radius, Angle = angle };
        public static AoeInfo Cone(float radius, float angle, Vector4 color) => new() { Shape = AoeShape.Cone, Radius = radius, Angle = angle, Color = color };

        public static readonly Dictionary<uint, AoeInfo> Abilities = new()
        {
            { 46520, Circle(60f) }, // DiversDareRed // RedHot->self
            { 46521, Circle(60f) }, // DiversDareBlue // DeepBlue->self
            { 46518, Circle(6f) }, // HotImpact1 // RedHot->players
            { 46464, Circle(6f) }, // HotImpact2 // RedHot->players
            { 46523, Rect(60f, 8f) }, // FlameFloater1 // RedHot->location
            { 46524, Rect(60f, 8f) }, // FlameFloater2 // RedHot->location
            { 46525, Rect(60f, 8f) }, // FlameFloater3 // RedHot->location
            { 46526, Rect(60f, 8f) }, // FlameFloater4 // RedHot->location
            { 46529, Circle(5f) }, // AlleyOopInferno // Helper->player
            { 46538, Cone(60f, 330f) }, // CutbackBlaze // Helper->self
            { 46531, Circle(6f) }, // PyrotationSpread // Helper->players
            { 46540, Rect(50f, 50f) }, // SickSwellAOE // Helper->self
            { 46542, Rect(50f, 15f) }, // SickestTakeOffAOE // Helper->self
            { 46543, Circle(5f) }, // AwesomeSplash1 // Helper->players
            { 46544, Circle(6f) }, // AwesomeSlab1 // Helper->players
            { 46551, Circle(5f) }, // AwesomeSplash2 // Helper->player
            { 46552, Circle(6f) }, // AwesomeSlab2 // Helper->players
            { 46558, Cone(60f, 30f) }, // AlleyOopDoubleDipFirst // Helper->self
            { 46559, Cone(60f, 15f) }, // AlleyOopDoubleDipRepeat // Helper->self
            { 44486, Circle(6f) }, // DeepImpact // DeepBlue->players
            { 46500, Rect(50f, 40f) }, // XtremeSpectacularProximity // TheXtremes->self
            { 46556, Circle(60f) }, // XtremeSpectacularRepeat // TheXtremes->self
            { 47050, Circle(60f) }, // XtremeSpectacularFinal // TheXtremes->self
            { 46577, Cone(60f, 45f) }, // BlastingSnapAOE // Helper->self
            { 46578, Cone(60f, 45f) }, // PlungingSnapAOE // Helper->self
            { 46585, Circle(6f) }, // VerticalBlastAOE // Helper->players
            { 46586, Circle(6f) }, // VerticalPlungeAOE // Helper->player
            { 45953, Circle(60f) }, // Firesnaking // RedHot->self
            { 45954, Circle(60f) }, // Watersnaking // DeepBlue->self
            { 46587, Circle(9f) }, // SteamBurst // XtremeAether->self
            { 46547, Cone(60f, 120f) }, // DeepVarialAOE // Helper->self
            { 47390, Circle(6f) }, // HotAerialSpread1 // Helper->player
            { 47391, Circle(6f) }, // HotAerialSpread2 // Helper->player
            { 47392, Circle(6f) }, // HotAerialSpread3 // Helper->player
            { 47393, Circle(6f) }, // HotAerialSpread4 // Helper->players
            { 46564, Circle(6f) }, // DeepAerialTower // Helper->self
            { 46565, Circle(60f) }, // UnmitigatedExplosion // Helper->self
            { 46545, Rect(60f, 8f) }, // XtremeWaveRedRect // RedHot->location
            { 46546, Rect(60f, 8f) }, // XtremeWaveBlueRect // DeepBlue->location
            { 44487, Circle(60f) }, // ScathingSteam // WateryGrave->self
            { 46571, Circle(60f) }, // ImpactZone1 // WateryGrave->self
            { 46572, Circle(60f) }, // ImpactZone2 // WateryGrave->self
            { 46527, Rect(60f, 8f) }, // FlameFloaterSplitRect // RedHot->location
            { 46487, Circle(6f) }, // FreakyPyrotationStack // Helper->player
            { 46510, Circle(60f) }, // XtremeFiresnaking // RedHot->self
            { 46511, Circle(60f) }, // XtremeWatersnaking // DeepBlue->self
            { 46512, Circle(15f) }, // Bailout1 // Helper->players
            { 46513, Circle(15f) }, // Bailout2 // Helper->players
            { 46588, Circle(60f) }, // OverTheFallsRed // RedHot->self
            { 46589, Circle(60f) }, // OverTheFallsBlue // DeepBlue->self
        };
    }
}