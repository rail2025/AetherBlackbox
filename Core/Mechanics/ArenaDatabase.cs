using AetherBlackbox.DrawingLogic;
using System.Collections.Generic;
using System.Numerics;

namespace AetherBlackbox.Core.Mechanics
{
    public static class ArenaDatabase
    {
        private static readonly Dictionary<uint, ArenaDefinition> Definitions = new()
        {
            {
                992, new ArenaDefinition
                {
                    TerritoryId = 992,
                    Visuals = new Dictionary<string, ArenaVisual> { { "P1", new ArenaVisual { TexturePath = "PluginImages/arenas/m9.webp", Scale = 1.0f, AnchorToWaymarks = true } } },
                    Phases = new List<ArenaPhase> { new ArenaPhase("P1", TriggerType.Ability, new List<string>()) }
                }
            },
            {
                1321, new ArenaDefinition
                {
                    TerritoryId = 1321,
                    Visuals = new Dictionary<string, ArenaVisual> { { "P1", new ArenaVisual { TexturePath = "PluginImages/arenas/m9.webp", Scale = 1.0f, AnchorToWaymarks = true } } },
                    Phases = new List<ArenaPhase> { new ArenaPhase("P1", TriggerType.Ability, new List<string>()) }
                }
            },
            {
                1323, new ArenaDefinition
                {
                    TerritoryId = 1323,
                    Visuals = new Dictionary<string, ArenaVisual> { { "P1", new ArenaVisual { TexturePath = "PluginImages/arenas/m10.webp", Scale = 1.0f, AnchorToWaymarks = true } } },
                    Phases = new List<ArenaPhase> { new ArenaPhase("P1", TriggerType.Ability, new List<string>()) }
                }
            },
            {
                1238, new ArenaDefinition
                {
                    TerritoryId = 1238,
                    Visuals = new Dictionary<string, ArenaVisual> { { "P1", new ArenaVisual { TexturePath = "PluginImages/arenas/fru.webp", Scale = 1.0f, AnchorToWaymarks = false } } },
                    Phases = new List<ArenaPhase> { new ArenaPhase("P1", TriggerType.Ability, new List<string>()) }
                }
            },
            {
                1327, new ArenaDefinition
                {
                    TerritoryId = 1327,
                    Visuals = new Dictionary<string, ArenaVisual>
                    {
                        { "P1", new ArenaVisual { TexturePath = "PluginImages/arenas/m12p1.webp", Scale = 1.0f } },
                        { "P2", new ArenaVisual { TexturePath = "PluginImages/arenas/m12p2.webp", Scale = 1.0f } }
                    },
                    Phases = new List<ArenaPhase>
                    {
                        new ArenaPhase("P2", TriggerType.Ability, new List<string> { "Aflame" })
                    }
                }
            },
            {
                755, new ArenaDefinition
                {
                    TerritoryId = 755,
                    Visuals = new Dictionary<string, ArenaVisual>
                    {
                        { "P1", new ArenaVisual { TexturePath = "PluginImages/arenas/p1_fg.webp", Scale = 1.0f } },
                        { "P2", new ArenaVisual { TexturePath = "PluginImages/arenas/p2_fg.webp", Scale = 1.0f } }
                    },
                    Phases = new List<ArenaPhase>
                    {
                        new ArenaPhase("P2", TriggerType.Ability, new List<string> { "Heartless Angel" })
                    }
                }
            },
            {
                1325, new ArenaDefinition
                {
                    TerritoryId = 1325,
                    Visuals = new Dictionary<string, ArenaVisual>
                    {
                        { "P1", new ArenaVisual { TexturePath = "PluginImages/arenas/m11p1.webp", Scale = 1.0f } },
                        { "P2", new ArenaVisual { TexturePath = "PluginImages/arenas/m11p2.webp", Scale = 1.0f } }
                    },
                    Phases = new List<ArenaPhase>
                    {
                        new ArenaPhase("P2", TriggerType.Ability, new List<string> { "Flatliner" }),
                        new ArenaPhase("P1", TriggerType.Ability, new List<string> { "Ecliptic Stampede" })
                    }
                }
            }
            /* ============================
             * TEMPLATE FOR FUTURE ARENAS
             * ============================
            ,
            {
                9999, new ArenaDefinition // Replace 9999 with the actual TerritoryId
                {
                    TerritoryId = 9999,
                    Visuals = new Dictionary<string, ArenaVisual>
                    {
                        // Add phase visual mappings here. Scale and Offset are optional.
                        { "P1", new ArenaVisual { Texture = TextureManager.GetTexture("PluginImages/arenas/default_p1.webp")!, Scale = 1.0f } },
                        { "P2", new ArenaVisual { Texture = TextureManager.GetTexture("PluginImages/arenas/default_p2.webp")!, Scale = 1.0f, AnchorToWaymarks = true } }
                    },
                    Phases = new List<ArenaPhase>
                    {
                        // TriggerType can be Ability, StatusGain, StatusLoss, Custom
                        // Include all possible localized/difficulty names in the TriggerNames list
                        new ArenaPhase("P2", TriggerType.Ability, new List<string> { "Phase 2 Attack Name", "Alternate Attack Name" })
                    }
                }
            }
            */
        };

        public static ArenaDefinition? Get(uint territoryId)
        {
            return Definitions.GetValueOrDefault(territoryId);
        }
    }
}