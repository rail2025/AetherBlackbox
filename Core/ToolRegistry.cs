using AetherBlackbox.DrawingLogic;
using System.Collections.Generic;
using System.Numerics;

namespace AetherBlackbox.Core
{
    public class ToolMetadata
    {
        public string DisplayName { get; set; } = "";
        public string IconPath { get; set; } = "";
        public string CanvasImagePath { get; set; } = "";
        public Vector2 DefaultSize { get; set; } = new Vector2(30f, 30f);
        public bool IsPlaceableImage { get; set; } = false;
    }

    public static class ToolRegistry
    {
        public static readonly Dictionary<DrawMode, ToolMetadata> Tools = new()
        {
            // Basic
            { DrawMode.Pen, new ToolMetadata { DisplayName = "Pen" } },
            { DrawMode.StraightLine, new ToolMetadata { DisplayName = "Line" } },
            { DrawMode.Dash, new ToolMetadata { DisplayName = "Dash" } },
            { DrawMode.Rectangle, new ToolMetadata { DisplayName = "Rect" } },
            { DrawMode.Circle, new ToolMetadata { DisplayName = "Circle" } },
            { DrawMode.Donut, new ToolMetadata { DisplayName = "Donut" } },
            { DrawMode.Arrow, new ToolMetadata { DisplayName = "Arrow" } },
            { DrawMode.Cone, new ToolMetadata { DisplayName = "Cone" } },
            { DrawMode.Triangle, new ToolMetadata { DisplayName = "Triangle" } },
            { DrawMode.Pie, new ToolMetadata { DisplayName = "Pie" } },
            { DrawMode.Starburst, new ToolMetadata { DisplayName = "Star", IconPath = "PluginImages.svg.starburst.png" } },
            { DrawMode.TextTool, new ToolMetadata { DisplayName = "TEXT" } },
            
            // Image
            { DrawMode.Image, new ToolMetadata { DisplayName = "IMG", IconPath = "PluginImages.toolbar.Square.png", CanvasImagePath = "PluginImages.toolbar.Square.png", DefaultSize = new Vector2(25f, 25f), IsPlaceableImage = true } },

            // Mechanics
            { DrawMode.BossImage, new ToolMetadata { IconPath = "PluginImages.svg.boss.svg", CanvasImagePath = "PluginImages.svg.boss.svg", DefaultSize = new Vector2(60f, 60f), IsPlaceableImage = true } },
            { DrawMode.CircleAoEImage, new ToolMetadata { IconPath = "PluginImages.svg.prox_aoe.svg", CanvasImagePath = "PluginImages.svg.prox_aoe.svg", DefaultSize = new Vector2(80f, 80f), IsPlaceableImage = true } },
            { DrawMode.DonutAoEImage, new ToolMetadata { IconPath = "PluginImages.svg.donut.svg", CanvasImagePath = "PluginImages.svg.donut.svg", DefaultSize = new Vector2(100f, 100f), IsPlaceableImage = true } },
            { DrawMode.FlareImage, new ToolMetadata { IconPath = "PluginImages.svg.flare.svg", CanvasImagePath = "PluginImages.svg.flare.svg", DefaultSize = new Vector2(60f, 60f), IsPlaceableImage = true } },
            { DrawMode.LineStackImage, new ToolMetadata { IconPath = "PluginImages.svg.line_stack.svg", CanvasImagePath = "PluginImages.svg.line_stack.svg", DefaultSize = new Vector2(30f, 60f), IsPlaceableImage = true } },
            { DrawMode.SpreadImage, new ToolMetadata { IconPath = "PluginImages.svg.spread.svg", CanvasImagePath = "PluginImages.svg.spread.svg", DefaultSize = new Vector2(60f, 60f), IsPlaceableImage = true } },
            { DrawMode.StackImage, new ToolMetadata { IconPath = "PluginImages.svg.stack.svg", CanvasImagePath = "PluginImages.svg.stack.svg", DefaultSize = new Vector2(60f, 60f), IsPlaceableImage = true } },
            { DrawMode.GazeImage, new ToolMetadata { IconPath = "PluginImages.svg.gaze.png", CanvasImagePath = "PluginImages.svg.gaze.png", DefaultSize = new Vector2(80f, 80f), IsPlaceableImage = true } },
            { DrawMode.TowerImage, new ToolMetadata { IconPath = "PluginImages.svg.tower.png", CanvasImagePath = "PluginImages.svg.tower.png", DefaultSize = new Vector2(80f, 80f), IsPlaceableImage = true } },
            { DrawMode.ExasImage, new ToolMetadata { IconPath = "PluginImages.svg.exas.svg", CanvasImagePath = "PluginImages.svg.exas.svg", DefaultSize = new Vector2(80f, 80f), IsPlaceableImage = true } },

            // Shapes (Placeable)
            { DrawMode.SquareImage, new ToolMetadata { DisplayName = "Square", IconPath = "PluginImages.toolbar.Square.png", CanvasImagePath = "PluginImages.toolbar.Square.png", DefaultSize = new Vector2(40f, 40f), IsPlaceableImage = true } },
            { DrawMode.CircleMarkImage, new ToolMetadata { DisplayName = "CircleMark", IconPath = "PluginImages.toolbar.CircleMark.png", CanvasImagePath = "PluginImages.toolbar.CircleMark.png", DefaultSize = new Vector2(40f, 40f), IsPlaceableImage = true } },
            { DrawMode.TriangleImage, new ToolMetadata { DisplayName = "Triangle", IconPath = "PluginImages.toolbar.Triangle.png", CanvasImagePath = "PluginImages.toolbar.Triangle.png", DefaultSize = new Vector2(40f, 40f), IsPlaceableImage = true } },
            { DrawMode.PlusImage, new ToolMetadata { DisplayName = "Plus", IconPath = "PluginImages.toolbar.Plus.png", CanvasImagePath = "PluginImages.toolbar.Plus.png", DefaultSize = new Vector2(40f, 40f), IsPlaceableImage = true } },

            // Roles
            { DrawMode.RoleTankImage, new ToolMetadata { DisplayName = "Tank", IconPath = "PluginImages.toolbar.Tank.JPG", CanvasImagePath = "PluginImages.toolbar.Tank.JPG", IsPlaceableImage = true } },
            { DrawMode.RoleHealerImage, new ToolMetadata { DisplayName = "Healer", IconPath = "PluginImages.toolbar.Healer.JPG", CanvasImagePath = "PluginImages.toolbar.Healer.JPG", IsPlaceableImage = true } },
            { DrawMode.RoleMeleeImage, new ToolMetadata { DisplayName = "Melee", IconPath = "PluginImages.toolbar.Melee.JPG", CanvasImagePath = "PluginImages.toolbar.Melee.JPG", IsPlaceableImage = true } },
            { DrawMode.RoleRangedImage, new ToolMetadata { DisplayName = "Ranged", IconPath = "PluginImages.toolbar.Ranged.JPG", CanvasImagePath = "PluginImages.toolbar.Ranged.JPG", IsPlaceableImage = true } },
            { DrawMode.RoleCasterImage, new ToolMetadata { DisplayName = "Caster", IconPath = "PluginImages.toolbar.caster.png", CanvasImagePath = "PluginImages.toolbar.caster.png", IsPlaceableImage = true } },

            // Party
            { DrawMode.Party1Image, new ToolMetadata { DisplayName = "Party 1", IconPath = "PluginImages.toolbar.Party1.png", CanvasImagePath = "PluginImages.toolbar.Party1.png", IsPlaceableImage = true } },
            { DrawMode.Party2Image, new ToolMetadata { DisplayName = "Party 2", IconPath = "PluginImages.toolbar.Party2.png", CanvasImagePath = "PluginImages.toolbar.Party2.png", IsPlaceableImage = true } },
            { DrawMode.Party3Image, new ToolMetadata { DisplayName = "Party 3", IconPath = "PluginImages.toolbar.Party3.png", CanvasImagePath = "PluginImages.toolbar.Party3.png", IsPlaceableImage = true } },
            { DrawMode.Party4Image, new ToolMetadata { DisplayName = "Party 4", IconPath = "PluginImages.toolbar.Party4.png", CanvasImagePath = "PluginImages.toolbar.Party4.png", IsPlaceableImage = true } },
            { DrawMode.Party5Image, new ToolMetadata { DisplayName = "Party 5", IconPath = "PluginImages.toolbar.Party5.png", CanvasImagePath = "PluginImages.toolbar.Party5.png", IsPlaceableImage = true } },
            { DrawMode.Party6Image, new ToolMetadata { DisplayName = "Party 6", IconPath = "PluginImages.toolbar.Party6.png", CanvasImagePath = "PluginImages.toolbar.Party6.png", IsPlaceableImage = true } },
            { DrawMode.Party7Image, new ToolMetadata { DisplayName = "Party 7", IconPath = "PluginImages.toolbar.Party7.png", CanvasImagePath = "PluginImages.toolbar.Party7.png", IsPlaceableImage = true } },
            { DrawMode.Party8Image, new ToolMetadata { DisplayName = "Party 8", IconPath = "PluginImages.toolbar.Party8.png", CanvasImagePath = "PluginImages.toolbar.Party8.png", IsPlaceableImage = true } },
            { DrawMode.Bind1Image, new ToolMetadata { DisplayName = "Bind 1", IconPath = "PluginImages.toolbar.bind1.png", CanvasImagePath = "PluginImages.toolbar.bind1.png", IsPlaceableImage = true } },
            { DrawMode.Bind2Image, new ToolMetadata { DisplayName = "Bind 2", IconPath = "PluginImages.toolbar.bind2.png", CanvasImagePath = "PluginImages.toolbar.bind2.png", IsPlaceableImage = true } },
            { DrawMode.Bind3Image, new ToolMetadata { DisplayName = "Bind 3", IconPath = "PluginImages.toolbar.bind3.png", CanvasImagePath = "PluginImages.toolbar.bind3.png", IsPlaceableImage = true } },
            { DrawMode.Ignore1Image, new ToolMetadata { DisplayName = "Ignore 1", IconPath = "PluginImages.toolbar.ignore1.png", CanvasImagePath = "PluginImages.toolbar.ignore1.png", IsPlaceableImage = true } },
            { DrawMode.Ignore2Image, new ToolMetadata { DisplayName = "Ignore 2", IconPath = "PluginImages.toolbar.ignore2.png", CanvasImagePath = "PluginImages.toolbar.ignore2.png", IsPlaceableImage = true } },

            // Dots
            { DrawMode.Dot1Image, new ToolMetadata { DisplayName = "Dot 1", IconPath = "PluginImages.svg.1dot.svg", CanvasImagePath = "PluginImages.svg.1dot.svg", IsPlaceableImage = true } },
            { DrawMode.Dot2Image, new ToolMetadata { DisplayName = "Dot 2", IconPath = "PluginImages.svg.2dot.svg", CanvasImagePath = "PluginImages.svg.2dot.svg", IsPlaceableImage = true } },
            { DrawMode.Dot3Image, new ToolMetadata { DisplayName = "Dot 3", IconPath = "PluginImages.svg.3dot.svg", CanvasImagePath = "PluginImages.svg.3dot.svg", IsPlaceableImage = true } },
            { DrawMode.Dot4Image, new ToolMetadata { DisplayName = "Dot 4", IconPath = "PluginImages.svg.4dot.svg", CanvasImagePath = "PluginImages.svg.4dot.svg", IsPlaceableImage = true } },
            { DrawMode.Dot5Image, new ToolMetadata { DisplayName = "Dot 5", IconPath = "PluginImages.svg.5dot.svg", CanvasImagePath = "PluginImages.svg.5dot.svg", IsPlaceableImage = true } },
            { DrawMode.Dot6Image, new ToolMetadata { DisplayName = "Dot 6", IconPath = "PluginImages.svg.6dot.svg", CanvasImagePath = "PluginImages.svg.6dot.svg", IsPlaceableImage = true } },
            { DrawMode.Dot7Image, new ToolMetadata { DisplayName = "Dot 7", IconPath = "PluginImages.svg.7dot.svg", CanvasImagePath = "PluginImages.svg.7dot.svg", IsPlaceableImage = true } },
            { DrawMode.Dot8Image, new ToolMetadata { DisplayName = "Dot 8", IconPath = "PluginImages.svg.8dot.svg", CanvasImagePath = "PluginImages.svg.8dot.svg", IsPlaceableImage = true } },

            // Capture Auto-Generated / Specific Uses
            { DrawMode.StatusIconPlaceholder, new ToolMetadata { IconPath = "PluginImages.toolbar.StatusPlaceholder.png", CanvasImagePath = "PluginImages.toolbar.StatusPlaceholder.png", DefaultSize = new Vector2(40f, 40f), IsPlaceableImage = true } },
            { DrawMode.RoleTank1Image, new ToolMetadata { IconPath = "PluginImages.toolbar.tank_1.png", CanvasImagePath = "PluginImages.toolbar.tank_1.png", IsPlaceableImage = true } },
            { DrawMode.RoleTank2Image, new ToolMetadata { IconPath = "PluginImages.toolbar.tank_2.png", CanvasImagePath = "PluginImages.toolbar.tank_2.png", IsPlaceableImage = true } },
            { DrawMode.RoleHealer1Image, new ToolMetadata { IconPath = "PluginImages.toolbar.healer_1.png", CanvasImagePath = "PluginImages.toolbar.healer_1.png", IsPlaceableImage = true } },
            { DrawMode.RoleHealer2Image, new ToolMetadata { IconPath = "PluginImages.toolbar.healer_2.png", CanvasImagePath = "PluginImages.toolbar.healer_2.png", IsPlaceableImage = true } },
            { DrawMode.RoleMelee1Image, new ToolMetadata { IconPath = "PluginImages.toolbar.melee_1.png", CanvasImagePath = "PluginImages.toolbar.melee_1.png", IsPlaceableImage = true } },
            { DrawMode.RoleMelee2Image, new ToolMetadata { IconPath = "PluginImages.toolbar.melee_2.png", CanvasImagePath = "PluginImages.toolbar.melee_2.png", IsPlaceableImage = true } },
            { DrawMode.RoleRanged1Image, new ToolMetadata { IconPath = "PluginImages.toolbar.ranged_dps_1.png", CanvasImagePath = "PluginImages.toolbar.ranged_dps_1.png", IsPlaceableImage = true } },
            { DrawMode.RoleRanged2Image, new ToolMetadata { IconPath = "PluginImages.toolbar.ranged_dps_2.png", CanvasImagePath = "PluginImages.toolbar.ranged_dps_2.png", IsPlaceableImage = true } },

            { DrawMode.WaymarkAImage, new ToolMetadata { IconPath = "PluginImages.toolbar.A.png", CanvasImagePath = "PluginImages.toolbar.A.png", DefaultSize = new Vector2(40f, 40f), IsPlaceableImage = true } },
            { DrawMode.WaymarkBImage, new ToolMetadata { IconPath = "PluginImages.toolbar.B.png", CanvasImagePath = "PluginImages.toolbar.B.png", DefaultSize = new Vector2(40f, 40f), IsPlaceableImage = true } },
            { DrawMode.WaymarkCImage, new ToolMetadata { IconPath = "PluginImages.toolbar.C.png", CanvasImagePath = "PluginImages.toolbar.C.png", DefaultSize = new Vector2(40f, 40f), IsPlaceableImage = true } },
            { DrawMode.WaymarkDImage, new ToolMetadata { IconPath = "PluginImages.toolbar.D.png", CanvasImagePath = "PluginImages.toolbar.D.png", DefaultSize = new Vector2(40f, 40f), IsPlaceableImage = true } },
            { DrawMode.Waymark1Image, new ToolMetadata { IconPath = "PluginImages.toolbar.1_waymark.png", CanvasImagePath = "PluginImages.toolbar.1_waymark.png", DefaultSize = new Vector2(40f, 40f), IsPlaceableImage = true } },
            { DrawMode.Waymark2Image, new ToolMetadata { IconPath = "PluginImages.toolbar.2_waymark.png", CanvasImagePath = "PluginImages.toolbar.2_waymark.png", DefaultSize = new Vector2(40f, 40f), IsPlaceableImage = true } },
            { DrawMode.Waymark3Image, new ToolMetadata { IconPath = "PluginImages.toolbar.3_waymark.png", CanvasImagePath = "PluginImages.toolbar.3_waymark.png", DefaultSize = new Vector2(40f, 40f), IsPlaceableImage = true } },
            { DrawMode.Waymark4Image, new ToolMetadata { IconPath = "PluginImages.toolbar.4_waymark.png", CanvasImagePath = "PluginImages.toolbar.4_waymark.png", DefaultSize = new Vector2(40f, 40f), IsPlaceableImage = true } },

            // Jobs
            { DrawMode.JobPldImage, new ToolMetadata { IconPath = "PluginImages.toolbar.pld.png", CanvasImagePath = "PluginImages.toolbar.pld.png", IsPlaceableImage = true } },
            { DrawMode.JobWarImage, new ToolMetadata { IconPath = "PluginImages.toolbar.war.png", CanvasImagePath = "PluginImages.toolbar.war.png", IsPlaceableImage = true } },
            { DrawMode.JobDrkImage, new ToolMetadata { IconPath = "PluginImages.toolbar.drk.png", CanvasImagePath = "PluginImages.toolbar.drk.png", IsPlaceableImage = true } },
            { DrawMode.JobGnbImage, new ToolMetadata { IconPath = "PluginImages.toolbar.gnb.png", CanvasImagePath = "PluginImages.toolbar.gnb.png", IsPlaceableImage = true } },
            { DrawMode.JobWhmImage, new ToolMetadata { IconPath = "PluginImages.toolbar.whm.png", CanvasImagePath = "PluginImages.toolbar.whm.png", IsPlaceableImage = true } },
            { DrawMode.JobSchImage, new ToolMetadata { IconPath = "PluginImages.toolbar.sch.png", CanvasImagePath = "PluginImages.toolbar.sch.png", IsPlaceableImage = true } },
            { DrawMode.JobAstImage, new ToolMetadata { IconPath = "PluginImages.toolbar.ast.png", CanvasImagePath = "PluginImages.toolbar.ast.png", IsPlaceableImage = true } },
            { DrawMode.JobSgeImage, new ToolMetadata { IconPath = "PluginImages.toolbar.sge.png", CanvasImagePath = "PluginImages.toolbar.sge.png", IsPlaceableImage = true } },
            { DrawMode.JobMnkImage, new ToolMetadata { IconPath = "PluginImages.toolbar.mnk.png", CanvasImagePath = "PluginImages.toolbar.mnk.png", IsPlaceableImage = true } },
            { DrawMode.JobDrgImage, new ToolMetadata { IconPath = "PluginImages.toolbar.drg.png", CanvasImagePath = "PluginImages.toolbar.drg.png", IsPlaceableImage = true } },
            { DrawMode.JobNinImage, new ToolMetadata { IconPath = "PluginImages.toolbar.nin.png", CanvasImagePath = "PluginImages.toolbar.nin.png", IsPlaceableImage = true } },
            { DrawMode.JobSamImage, new ToolMetadata { IconPath = "PluginImages.toolbar.sam.png", CanvasImagePath = "PluginImages.toolbar.sam.png", IsPlaceableImage = true } },
            { DrawMode.JobRprImage, new ToolMetadata { IconPath = "PluginImages.toolbar.rpr.png", CanvasImagePath = "PluginImages.toolbar.rpr.png", IsPlaceableImage = true } },
            { DrawMode.JobVprImage, new ToolMetadata { IconPath = "PluginImages.toolbar.vpr.png", CanvasImagePath = "PluginImages.toolbar.vpr.png", IsPlaceableImage = true } },
            { DrawMode.JobBrdImage, new ToolMetadata { IconPath = "PluginImages.toolbar.brd.png", CanvasImagePath = "PluginImages.toolbar.brd.png", IsPlaceableImage = true } },
            { DrawMode.JobMchImage, new ToolMetadata { IconPath = "PluginImages.toolbar.mch.png", CanvasImagePath = "PluginImages.toolbar.mch.png", IsPlaceableImage = true } },
            { DrawMode.JobDncImage, new ToolMetadata { IconPath = "PluginImages.toolbar.dnc.png", CanvasImagePath = "PluginImages.toolbar.dnc.png", IsPlaceableImage = true } },
            { DrawMode.JobBlmImage, new ToolMetadata { IconPath = "PluginImages.toolbar.blm.png", CanvasImagePath = "PluginImages.toolbar.blm.png", IsPlaceableImage = true } },
            { DrawMode.JobSmnImage, new ToolMetadata { IconPath = "PluginImages.toolbar.smn.png", CanvasImagePath = "PluginImages.toolbar.smn.png", IsPlaceableImage = true } },
            { DrawMode.JobRdmImage, new ToolMetadata { IconPath = "PluginImages.toolbar.rdm.png", CanvasImagePath = "PluginImages.toolbar.rdm.png", IsPlaceableImage = true } },
            { DrawMode.JobPctImage, new ToolMetadata { IconPath = "PluginImages.toolbar.pct.png", CanvasImagePath = "PluginImages.toolbar.pct.png", IsPlaceableImage = true } },

            // Arenas
            { DrawMode.ArenaM9, new ToolMetadata { IconPath = "PluginImages.toolbar.m9.png", CanvasImagePath = "PluginImages.toolbar.m9.png", DefaultSize = new Vector2(512f, 512f), IsPlaceableImage = true } },
            { DrawMode.ArenaM10, new ToolMetadata { IconPath = "PluginImages.toolbar.m10.png", CanvasImagePath = "PluginImages.toolbar.m10.png", DefaultSize = new Vector2(512f, 512f), IsPlaceableImage = true } },
            { DrawMode.ArenaM11P1, new ToolMetadata { IconPath = "PluginImages.toolbar.m11p1.png", CanvasImagePath = "PluginImages.toolbar.m11p1.png", DefaultSize = new Vector2(512f, 512f), IsPlaceableImage = true } },
            { DrawMode.ArenaM11P2, new ToolMetadata { IconPath = "PluginImages.toolbar.m11p2.png", CanvasImagePath = "PluginImages.toolbar.m11p2.png", DefaultSize = new Vector2(512f, 512f), IsPlaceableImage = true } },
            { DrawMode.ArenaM12P1, new ToolMetadata { IconPath = "PluginImages.toolbar.m12p1.png", CanvasImagePath = "PluginImages.toolbar.m12p1.png", DefaultSize = new Vector2(512f, 512f), IsPlaceableImage = true } },
            { DrawMode.ArenaM12P2, new ToolMetadata { IconPath = "PluginImages.toolbar.m12p2.png", CanvasImagePath = "PluginImages.toolbar.m12p2.png", DefaultSize = new Vector2(512f, 512f), IsPlaceableImage = true } },

            { DrawMode.EmojiImage, new ToolMetadata { IsPlaceableImage = true } }
        };
    }
}