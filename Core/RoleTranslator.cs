using System.Collections.Generic;
using System.Linq;
using AetherBlackbox.DrawingLogic;
using AetherBlackbox.Core;

namespace AetherBlackbox.Core
{
    public class RoleTranslator
    {
        private readonly Dictionary<ulong, DrawMode> _cachedRoleMap = new();

        public IReadOnlyDictionary<ulong, DrawMode> CachedRoleMap => _cachedRoleMap;

        public void CacheRoleMapping(ReplayRecording recording)
        {
            _cachedRoleMap.Clear();

            if (recording == null || recording.Metadata == null) return;

            var sortedParty = recording.Metadata
                .Where(kvp => kvp.Value.Type == EntityType.Player)
                .OrderBy(kvp => GetRolePriority(kvp.Value.ClassJobId))
                .ThenBy(kvp => kvp.Key)
                .ToList();

            int tankCount = 1;
            int healerCount = 1;
            int meleeCount = 1;
            int rangedCount = 1;

            foreach (var kvp in sortedParty)
            {
                _cachedRoleMap[kvp.Key] = AssignRole(kvp.Value.ClassJobId, ref tankCount, ref healerCount, ref meleeCount, ref rangedCount);
            }

            var enemies = recording.Metadata.Where(kvp => kvp.Value.Type == EntityType.Boss).ToList();
            foreach (var enemy in enemies)
            {
                _cachedRoleMap[enemy.Key] = DrawMode.BossImage;
            }
        }

        private int GetRolePriority(uint classJobId)
        {
            // 1: Tanks, 2: Healers, 3: Melee, 4: Physical Ranged, 5: Magical Ranged
            return classJobId switch
            {
                19 or 21 or 32 or 37 => 1, // PLD, WAR, DRK, GNB
                24 or 28 or 33 or 40 => 2, // WHM, SCH, AST, SGE
                20 or 22 or 30 or 34 or 39 or 41 => 3, // MNK, DRG, NIN, SAM, RPR, VPR
                23 or 31 or 38 => 4, // BRD, MCH, DNC
                25 or 27 or 35 or 42 => 5, // BLM, SMN, RDM, PCT
                _ => 99
            };
        }

        private DrawMode AssignRole(uint classJobId, ref int tankCount, ref int healerCount, ref int meleeCount, ref int rangedCount)
        {
            int priority = GetRolePriority(classJobId);
            return priority switch
            {
                1 => tankCount++ == 1 ? DrawMode.RoleTank1Image : DrawMode.RoleTank2Image,
                2 => healerCount++ == 1 ? DrawMode.RoleHealer1Image : DrawMode.RoleHealer2Image,
                3 => meleeCount++ == 1 ? DrawMode.RoleMelee1Image : DrawMode.RoleMelee2Image,
                4 or 5 => rangedCount++ == 1 ? DrawMode.RoleRanged1Image : DrawMode.RoleRanged2Image,
                _ => DrawMode.StatusIconPlaceholder
            };
        }
    }
}