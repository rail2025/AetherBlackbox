using AetherBlackbox.Game;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace AetherBlackbox.Events
{
    public static class CombatEventFactory
    {
        internal static CombatEvent.DamageTaken CreateDamage(
            CombatEvent.EventSnapshot snapshot, uint targetId, uint sourceId,
            uint actionId, uint amount, ActionEffectType effectType,
            byte param0, byte param1, ActionType displayType)
        {
            var action = GameDataCache.GetAction(actionId);
            return new CombatEvent.DamageTaken
            {
                Snapshot = snapshot,
                TargetActorId = targetId,
                SourceActorId = sourceId,
                ActionId = actionId,
                Amount = amount,
                Action = action?.ActionCategory.RowId == 1 ? "Auto-attack" : action?.Name.ExtractText() ?? "",
                Icon = action?.Icon,
                Crit = (param0 & 0x20) == 0x20,
                DirectHit = (param0 & 0x40) == 0x40,
                DamageType = (DamageType)(param1 & 0xF),
                Parried = effectType == ActionEffectType.ParriedDamage,
                Blocked = effectType == ActionEffectType.BlockedDamage,
                DisplayType = displayType
            };
        }

        public static CombatEvent.Healed CreateHealed(
            CombatEvent.EventSnapshot snapshot, uint targetId, uint sourceId,
            uint actionId, uint amount, bool isCrit)
        {
            var action = GameDataCache.GetAction(actionId);
            return new CombatEvent.Healed
            {
                Snapshot = snapshot,
                TargetActorId = targetId,
                SourceActorId = sourceId,
                Amount = amount,
                Action = action?.Name.ExtractText() ?? "",
                Icon = action?.Icon,
                Crit = isCrit
            };
        }

        public static CombatEvent.DoT CreateDoT(
            CombatEvent.EventSnapshot snapshot, uint targetId, uint sourceId,
            uint amount, uint actionId)
        {
            var status = GameDataCache.GetStatus(actionId);
            return new CombatEvent.DoT
            {
                Snapshot = snapshot,
                TargetActorId = targetId,
                SourceActorId = sourceId,
                Amount = amount,
                ActionId = actionId,
                Action = status?.Name.ExtractText() ?? ""
            };
        }

        public static CombatEvent.Healed CreateHoT(
            CombatEvent.EventSnapshot snapshot, uint targetId, uint sourceId,
            uint amount, uint actionId)
        {
            var status = GameDataCache.GetStatus(actionId);
            return new CombatEvent.Healed
            {
                Snapshot = snapshot,
                TargetActorId = targetId,
                SourceActorId = sourceId,
                Amount = amount,
                Action = status?.Name.ExtractText() ?? "",
                Icon = status?.Icon,
                Crit = false
            };
        }

        public static CombatEvent.StatusEffect CreateStatusEffect(
            CombatEvent.EventSnapshot snapshot, uint targetId, uint sourceId,
            uint effectId, uint stackCount, float duration)
        {
            var status = GameDataCache.GetStatus(effectId);
            return new CombatEvent.StatusEffect
            {
                Snapshot = snapshot,
                TargetActorId = targetId,
                Id = effectId,
                StackCount = stackCount <= status?.MaxStacks ? stackCount : 0u,
                Icon = status?.Icon,
                Status = status?.Name.ExtractText(),
                Description = status?.Description.ExtractText(),
                Category = (StatusCategory)(status?.StatusCategory ?? 0),
                SourceActorId = sourceId,
                Duration = duration
            };
        }
    }
}