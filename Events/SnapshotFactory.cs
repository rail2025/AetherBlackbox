using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.ClientState.Objects.SubKinds;

namespace AetherBlackbox.Events
{
    public static class SnapshotFactory
    {
        public static CombatEvent.EventSnapshot Create(IGameObject? targetObj, bool includeStatus = false, List<uint>? additionalStatus = null)
        {
            if (targetObj is IPlayerCharacter p)
            {
                var snap = p.Snapshot(includeStatus, additionalStatus);
                return snap with { Position = targetObj.Position };
            }

            uint currentHp = 0;
            uint maxHp = 0;
            if (targetObj is IBattleNpc npc)
            {
                currentHp = npc.CurrentHp;
                maxHp = npc.MaxHp;
            }

            return new CombatEvent.EventSnapshot
            {
                Time = DateTime.Now,
                CurrentHp = currentHp,
                MaxHp = maxHp,
                BarrierPercent = 0,
                Position = targetObj?.Position ?? Vector3.Zero
            };
        }
    }
}