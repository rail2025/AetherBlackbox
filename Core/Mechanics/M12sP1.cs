using System.Collections.Generic;
using System.Numerics;
using static AetherBlackbox.Core.Mechanics.AoeShape;

namespace AetherBlackbox.Core.Mechanics
{
    public static class M12sP1
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
            { 46295, Circle(60f) }, // TheFixer // Boss->self
            { 46230, Circle(6f) }, // MortalSlayerSpread // GreenOrb->players
            { 46232, Circle(6f) }, // MortalSlayerTank // RedOrb->players
            { 46238, Circle(5f) }, // PhagocyteSpotlightPlayer // Helper->location
            { 46237, Cone(35f, 120f) }, // RavenousReachCone // Helper->self
            { 46250, Circle(6f) }, // BurstingGrotesquerieDramaticLysis // Helper->location
            { 46254, Circle(6f) }, // SharedGrotesquerieFourthWallFusion // Helper->location
            { 46255, Cone(60f, 30f) }, // HemorrhagicProjection // Helper->self
            { 46239, Circle(12f) }, // Burst // Helper->location
            { 46294, Circle(6f) }, // VisceralBurst // Helper->players
            { 47545, Circle(6f) }, // Act1FourthWallFusion // Helper->players
            { 46262, Circle(5f) }, // PhagocyteSpotlightFixed // Helper->location
            { 46194, Circle(60f) }, // CruelCoilBind // Helper->self
            { 46398, Donut(13f, 9f) }, // SkinsplitterDonut // Helper->self
            { 46260, Circle(4f) }, // DramaticLysisTetherBreak // Helper->location
            { 46259, Circle(3f) }, // RoilingMass1 // BloodVessel->self
            { 46263, Circle(3f) }, // RoilingMass2 // BloodVessel->self
            { 46258, Circle(60f) }, // UnmitigatedExplosion // Helper->self
            { 46273, Circle(9f) }, // ConstrictorUnk // Helper->self
            { 46274, Circle(13f) }, // ConstrictorKill // Helper->self
            { 47558, Circle(60f) }, // SplattershedRaidwide // Helper->self
            { 46240, Circle(2f) }, // GrandEntranceAppear1 // FloorSnake1->self
            { 46241, Circle(2f) }, // GrandEntranceAppear2 // FloorSnake2->self
            { 46242, Circle(2f) }, // GrandEntranceAppear3 // FloorSnake3->self
            { 46243, Circle(2f) }, // GrandEntranceDisappear // Helper->self
            { 46244, Rect(10f, 20f) }, // BringDownTheHouseLarge // Helper->self
            { 46245, Rect(10f, 15f) }, // BringDownTheHouseMedium // Helper->self
            { 46246, Rect(10f, 10f) }, // BringDownTheHouseSmall // Helper->self
            { 46256, Circle(9f) }, // MitoticPhaseDramaticLysis // Helper->location
            { 47395, Circle(3f) }, // MetamitosisTower // Helper->self
            { 46251, Rect(60f, 10f) }, // SplitScourge // Helper->self
            { 46248, Circle(5f) }, // VenomousScourge // Helper->players
            { 46252, Circle(6f) }, // CellShedding // Helper->players
            { 44489, Circle(60f) }, // SlaughtershedRaidwide // Helper->self
            { 46292, Circle(6f) }, // CurtainCallDramaticLysis // Helper->players
            { 46293, Circle(6f) }, // CurtainCallFourthWallFusion // Helper->players
            { 47548, Rect(30f, 20f) }, // SerpentineScourgeRect // Helper->self
            { 47559, Circle(60f) }, // RaptorKnucklesKB // Helper->self
            { 45767, Circle(60f) }, // TheFixerEnrage // Boss->self
            { 46393, Circle(60f) }, // RefreshingOverkillRaidwide // Boss->self
            { 46394, Circle(60f) }, // RefreshingOverkillEnrage // Boss->self
            { 46395, Donut(30f, 20f) }, // P2ArenaTransition // Helper->self
            { 48028, Circle(60f) }, // P2Vacuum // Helper->self
        };
    }
}