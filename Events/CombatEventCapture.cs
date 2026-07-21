using AetherBlackbox.Core;
using AetherBlackbox.Game;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Hooking;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Network;
using System.Numerics;
using System;
using System.Collections.Generic;
using System.Linq;
using Action = Lumina.Excel.Sheets.Action;
using Status = Lumina.Excel.Sheets.Status;

namespace AetherBlackbox.Events;

public class CombatEventCapture : IDisposable
{
    private readonly Dictionary<ulong, List<CombatEvent>> combatEvents = new();
    private readonly Plugin plugin;

    private delegate void ProcessPacketEffectResultDelegate(uint targetId, IntPtr actionIntegrityData, byte isReplay);

    private readonly Hook<ActionEffectHandler.Delegates.Receive> processPacketActionEffectHook;
    private readonly Hook<PacketDispatcher.Delegates.HandleActorControlPacket> processPacketActorControlHook;

    [Signature("48 8B C4 44 88 40 18 89 48 08", DetourName = nameof(ProcessPacketEffectResultDetour))]
    private readonly Hook<ProcessPacketEffectResultDelegate> processPacketEffectResultHook = null!;

    public unsafe CombatEventCapture(Plugin plugin)
    {
        this.plugin = plugin;

        try
        {
            Service.GameInteropProvider.InitializeFromAttributes(this);
            processPacketEffectResultHook?.Enable();
        }
        catch (Exception e)
        {
            Service.PluginLog.Error(e, "Failed to hook EffectResult signatures.");
            Service.ChatGui.PrintError("[AetherBlackbox] Buffs/debuffs are currently not being tracked due to patch updates, but positional and actions are working.");
        }

        processPacketActionEffectHook = Service.GameInteropProvider.HookFromAddress(
            (nint)ActionEffectHandler.MemberFunctionPointers.Receive,
            new ActionEffectHandler.Delegates.Receive(ProcessPacketActionEffectDetour));
        processPacketActionEffectHook.Enable();

        processPacketActorControlHook = Service.GameInteropProvider.HookFromAddress(
            (nint)PacketDispatcher.MemberFunctionPointers.HandleActorControlPacket,
            new PacketDispatcher.Delegates.HandleActorControlPacket(ProcessPacketActorControlDetour));
        processPacketActorControlHook.Enable();
    }

    private void RegisterEntityMetadata(uint entityId, IGameObject obj)
    {
        if (!plugin.PullManager.IsInSession || plugin.PullManager.CurrentSession == null) return;
        var session = plugin.PullManager.CurrentSession;
        if (session.Metadata.ContainsKey(entityId)) return;

        string nameToStore = obj.Name.TextValue;
        uint classJobId = obj is ICharacter c ? c.ClassJob.RowId : 0;

        if (plugin.Configuration.AnonymizeNames && obj is IPlayerCharacter p)
        {
            var jobAbbr = p.ClassJob.Value.Abbreviation.ExtractText();
            if (string.IsNullOrEmpty(jobAbbr)) jobAbbr = "UNK";
            int count = 0;
            foreach (var meta in session.Metadata.Values)
            {
                if (meta.ClassJobId == classJobId) count++;
            }
            nameToStore = count == 0 ? jobAbbr : $"{jobAbbr} {count + 1}";
        }

        session.Metadata[entityId] = new ReplayMetadata
        {
            EntityId = entityId,
            ClassJobId = classJobId,
            Name = nameToStore,
            OwnerId = obj.OwnerId,
            Type = (EntityType)obj.ObjectKind
        };

        if (obj is IPlayerCharacter pc && (Service.ClientState.TerritoryType == 1197 || Service.ClientState.TerritoryType == 1252))
        {
            foreach (var status in pc.StatusList)
            {
                if (status.StatusId >= 4358 && status.StatusId <= 4805)
                {
                    var statusData = Service.DataManager.GetExcelSheet<Status>().GetRowOrDefault(status.StatusId);
                    combatEvents.AddEntry(entityId, new CombatEvent.StatusEffect
                    {
                        Snapshot = pc.Snapshot(),
                        TargetActorId = entityId,
                        Id = status.StatusId,
                        StackCount = 0,
                        SourceActorId = entityId,
                        Icon = statusData?.Icon,
                        Duration = 9999f,
                        Status = statusData?.Name.ExtractText(),
                        Description = statusData?.Description.ExtractText(),
                        Category = (StatusCategory)(statusData?.StatusCategory ?? 0)
                    });
                }
            }
        }
    }

    private unsafe void ProcessPacketActionEffectDetour(
        uint casterEntityId, Character* casterPtr, Vector3* targetPos, ActionEffectHandler.Header* effectHeader, ActionEffectHandler.TargetEffects* effectArray,
        GameObjectId* targetEntityIds)
    {
        processPacketActionEffectHook.Original(casterEntityId, casterPtr, targetPos, effectHeader, effectArray, targetEntityIds);

        try
        {
            if (effectHeader->NumTargets == 0)
                return;

            var actionId = (ActionType)effectHeader->ActionType switch
            {
                ActionType.Mount => 0xD000000 + effectHeader->ActionId,
                ActionType.Item => 0x2000000 + effectHeader->ActionId,
                _ => effectHeader->SpellId
            };

            Vector3? sourcePos = (targetPos != null) ? *targetPos : (casterPtr != null ? casterPtr->GameObject.Position : null);
            float? sourceRot = casterPtr != null ? casterPtr->GameObject.Rotation : null;
            plugin.PositionRecorder.OnActionUsed(casterEntityId, actionId, sourcePos, sourceRot);

            if (casterPtr != null)
            {
                if (Service.ObjectTable.SearchById(casterEntityId) is IGameObject casterObj)
                    RegisterEntityMetadata(casterEntityId, casterObj);
            }

            Action? action = null;
            List<uint>? additionalStatus = null;

            for (var i = 0; i < effectHeader->NumTargets; i++)
            {
                var actionTargetId = (uint)(targetEntityIds[i] & uint.MaxValue);

                var targetObj = Service.ObjectTable.SearchById(actionTargetId);
                var targetEffects = effectArray[i];

                if (plugin.PullManager.IsInSession && targetObj is IBattleNpc npc)
                {
                    for (var k = 0; k < 8; k++)
                    {
                        ref var eff = ref targetEffects.Effects[k];
                        var effectType = (ActionEffectType)eff.Type;

                        if (effectType is not (ActionEffectType.Damage or ActionEffectType.BlockedDamage or ActionEffectType.ParriedDamage))
                            continue;

                        uint dmg = eff.Value;
                        if ((eff.Param4 & 0x40) == 0x40)
                            dmg += (uint)eff.Param3 << 16;

                        if (dmg == 0)
                            continue;

                        action ??= Service.DataManager.GetExcelSheet<Action>().GetRowOrDefault(actionId);

                        plugin.PullManager.CurrentSession!.DamageByTarget.TryGetValue(npc.Name.TextValue, out var existing);
                        plugin.PullManager.CurrentSession.DamageByTarget[npc.Name.TextValue] = existing + dmg;

                        plugin.PullManager.CurrentSession.DetailedDamageEvents.Add(new CombatEvent.DamageTaken
                        {
                            Snapshot = new CombatEvent.EventSnapshot
                            {
                                Time = DateTime.Now,
                                CurrentHp = npc.CurrentHp,
                                MaxHp = npc.MaxHp,
                                BarrierPercent = 0
                            },
                            TargetActorId = actionTargetId,
                            SourceActorId = casterEntityId,
                            ActionId = effectHeader->ActionId,
                            Amount = dmg,
                            Action = action?.ActionCategory.RowId == 1 ? "Auto-attack" : action?.Name.ExtractText() ?? "",
                            Icon = action?.Icon,
                            Crit = (eff.Param0 & 0x20) == 0x20,
                            DirectHit = (eff.Param0 & 0x40) == 0x40,
                            DamageType = (DamageType)(eff.Param1 & 0xF),
                            Parried = effectType == ActionEffectType.ParriedDamage,
                            Blocked = effectType == ActionEffectType.BlockedDamage,
                            DisplayType = (ActionType)effectHeader->ActionType
                        });
                    }
                }
                if (!plugin.ConditionEvaluator.ShouldCapture(actionTargetId))
                    continue;

                if (targetObj == null)
                    continue;

                RegisterEntityMetadata(actionTargetId, targetObj);
                var p = targetObj as IPlayerCharacter;
                var npcTarget = targetObj as IBattleNpc;

                for (var j = 0; j < 8; j++)
                {
                    ref var actionEffect = ref targetEffects.Effects[j];
                    if (actionEffect.Type == 0)
                        continue;

                    uint amount = actionEffect.Value;
                    if ((actionEffect.Param4 & 0x40) == 0x40)
                        amount += (uint)actionEffect.Param3 << 16;

                    action ??= Service.DataManager.GetExcelSheet<Action>().GetRowOrDefault(actionId);

                    switch ((ActionEffectType)actionEffect.Type)
                    {
                        case ActionEffectType.Miss:
                        case ActionEffectType.Damage:
                        case ActionEffectType.BlockedDamage:
                        case ActionEffectType.ParriedDamage:
                            if (amount == 0 && (ActionEffectType)actionEffect.Type != ActionEffectType.Miss)
                                continue;

                            if (additionalStatus == null)
                            {
                                additionalStatus = [];

                                if (casterPtr != null)
                                {
                                    var statusManager = casterPtr->GetStatusManager();
                                    if (statusManager != null)
                                    {
                                        foreach (ref var status in statusManager->Status)
                                        {
                                            if (status.StatusId is 1203 or 1195 or 1193 or 860 or 1715 or 2115 or 3642)
                                                additionalStatus.Add(status.StatusId);
                                        }
                                    }
                                }
                            }

                            var pos = targetObj.Position;
                            var snapshot = p != null
                                ? p.Snapshot(true, additionalStatus) with { Position = pos }
                                : new CombatEvent.EventSnapshot { Time = DateTime.Now, CurrentHp = npcTarget?.CurrentHp ?? 0, MaxHp = npcTarget?.MaxHp ?? 0, BarrierPercent = 0, Position = pos };

                            combatEvents.AddEntry(actionTargetId,
                                new CombatEvent.DamageTaken
                                {
                                    // 1203 = Addle
                                    // 1195 = Feint
                                    // 1193 = Reprisal
                                    //  860 = Dismantled
                                    // 1715 = Malodorous, BLU Bad Breath
                                    // 2115 = Conked, BLU Magic Hammer
                                    // 3642 = Candy Cane, BLU Candy Cane
                                    Snapshot = snapshot,
                                    TargetActorId = actionTargetId,
                                    SourceActorId = casterEntityId,
                                    ActionId = effectHeader->ActionId,
                                    Amount = amount,
                                    Action = action?.ActionCategory.RowId == 1 ? "Auto-attack" : action?.Name.ExtractText() ?? "",
                                    Icon = action?.Icon,
                                    Crit = (actionEffect.Param0 & 0x20) == 0x20,
                                    DirectHit = (actionEffect.Param0 & 0x40) == 0x40,
                                    DamageType = (DamageType)(actionEffect.Param1 & 0xF),
                                    Parried = actionEffect.Type == (int)ActionEffectType.ParriedDamage,
                                    Blocked = actionEffect.Type == (int)ActionEffectType.BlockedDamage,
                                    DisplayType = (ActionType)effectHeader->ActionType
                                });
                            break;
                        case ActionEffectType.Heal:
                            if (amount == 0) continue;
                            combatEvents.AddEntry(actionTargetId,
                                new CombatEvent.Healed
                                {
                                    Snapshot = p.Snapshot(true),
                                    TargetActorId = actionTargetId,
                                    SourceActorId = casterEntityId,
                                    Amount = amount,
                                    Action = action?.Name.ExtractText() ?? "",
                                    Icon = action?.Icon,
                                    Crit = (actionEffect.Param1 & 0x20) == 0x20
                                });
                            break;
                    }
                }
            }
        }
        catch (Exception e)
        {
            Service.PluginLog.Error(e, "Caught unexpected exception");
        }
    }

    private void ProcessPacketActorControlDetour(
        uint entityId, uint category, uint param1, uint param2, uint param3, uint param4, uint param5, uint param6, uint param7, uint param8, GameObjectId targetId,
        bool param9)
    {
        processPacketActorControlHook.Original(entityId, category, param1, param2, param3, param4, param5, param6, param7, param8, targetId, param9);

        try
        {
            if (!plugin.ConditionEvaluator.ShouldCapture(entityId) && (ActorControlCategory)category != ActorControlCategory.DoT)
                return;
            var targetObj = Service.ObjectTable.SearchById(entityId);
            var p = targetObj as IPlayerCharacter;
            var npc = targetObj as IBattleNpc;

            if (p == null && npc == null)
                return;

            if (p != null)
                RegisterEntityMetadata(entityId, p);
            var snapshot = p != null
                ? p.Snapshot()
                : new CombatEvent.EventSnapshot { Time = DateTime.Now, CurrentHp = ((IBattleNpc)targetObj).CurrentHp, MaxHp = ((IBattleNpc)targetObj).MaxHp, BarrierPercent = 0 };

            switch ((ActorControlCategory)category)
            {
                case ActorControlCategory.DoT:
                    //Service.PluginLog.Info($"DoT packet received for Entity {entityId}, Amount {param2}");
                    var dotStatus = param1 != 0 ? Service.DataManager.GetExcelSheet<Status>().GetRowOrDefault(param1) : null;
                    uint sourceId = 0;
                    if (Service.ObjectTable.SearchById(entityId) is IBattleNpc battleNpc)
                    {
                        var status = battleNpc.StatusList.FirstOrDefault(s => s.StatusId == param1);
                        if (status != null) sourceId = status.SourceId;
                    }
                    var dotEvent = new CombatEvent.DoT
                    {
                        Snapshot = snapshot,
                        TargetActorId = entityId,
                        SourceActorId = sourceId,
                        Amount = param2,
                        ActionId = param1,
                        Action = dotStatus?.Name.ExtractText() ?? ""
                    };
                    combatEvents.AddEntry(entityId, new CombatEvent.DoT
                    {
                        Snapshot = snapshot,
                        TargetActorId = entityId,
                        SourceActorId = sourceId,
                        Amount = param2,
                        ActionId = param1,
                        Action = dotStatus?.Name.ExtractText() ?? ""
                    });
                    combatEvents.AddEntry(entityId, dotEvent);
                    if (plugin.PullManager.IsInSession && plugin.PullManager.CurrentSession != null)
                    {
                        plugin.PullManager.CurrentSession.DetailedDamageEvents.Add(dotEvent);
                    }
                    break;
                case ActorControlCategory.HoT:
                    if (param1 != 0)
                    {
                        var status = Service.DataManager.GetExcelSheet<Status>().GetRowOrDefault(param1);
                        combatEvents.AddEntry(entityId,
                            new CombatEvent.Healed
                            {
                                Snapshot = p.Snapshot(),
                                TargetActorId = entityId,
                                SourceActorId = entityId,
                                Amount = param2,
                                Action = status?.Name.ExtractText() ?? "",
                                Icon = status?.Icon,
                                Crit = param4 == 1
                            });
                    }
                    else
                    {
                        combatEvents.AddEntry(entityId, new CombatEvent.HoT { Snapshot = p.Snapshot(), TargetActorId = entityId, Amount = param2 });
                    }

                    break;
                case ActorControlCategory.Death:
                    {
                        Service.PluginLog.Info($"Packet: Death received for EntityId {entityId}");

                        if (combatEvents.Remove(entityId, out var events))
                        {
                            var replayData = plugin.PositionRecorder.GetReplayData();
                            var death = new Death
                            {
                                PlayerId = entityId,
                                TimeOfDeath = DateTime.Now,
                                Events = events,
                                ReplayData = replayData,
                                TerritoryTypeId = Service.ClientState.TerritoryType
                            };

                            Service.PluginLog.Info($"Capture: Created Death object for {entityId} at {death.TimeOfDeath:HH:mm:ss.fff}");

                            plugin.DeathsPerPlayer.AddEntry(entityId, death);
                            plugin.NotificationHandler.DisplayDeath(death);

                            plugin.PullManager.AddDeath(death);
                        }
                        else
                        {
                            Service.PluginLog.Info($"Capture: Death packet received but no combat events found for {entityId}");
                        }

                        break;
                    }
            }
        }
        catch (Exception e)
        {
            Service.PluginLog.Error(e, "Caught unexpected exception");
        }
    }

    private unsafe void ProcessPacketEffectResultDetour(uint targetId, IntPtr actionIntegrityData, byte isReplay)
    {
        processPacketEffectResultHook.Original(targetId, actionIntegrityData, isReplay);

        try
        {
            var message = (AddStatusEffect*)actionIntegrityData;

            var targetObj = Service.ObjectTable.SearchById(targetId);
            var p = targetObj as IPlayerCharacter;
            var npc = targetObj as IBattleNpc;

            if (p == null && npc == null)
                return;

            if (p != null)
            {
                if (!plugin.ConditionEvaluator.ShouldCapture(targetId))
                    return;

                RegisterEntityMetadata(targetId, p);
            }

            var snapshot = p != null
                ? p.Snapshot()
                : new CombatEvent.EventSnapshot
                {
                    Time = DateTime.Now,
                    CurrentHp = npc!.CurrentHp,
                    MaxHp = npc!.MaxHp,
                    BarrierPercent = 0
                };

            var effects = (StatusEffectAddEntry*)message->Effects;
            var effectCount = Math.Min(message->EffectCount, 4u);

            for (uint j = 0; j < effectCount; j++)
            {
                var effect = effects[j];
                var effectId = effect.EffectId;
                if (effectId <= 0 || effect.Duration < 0)
                    continue;

                if (Service.ObjectTable.SearchById(effect.SourceActorId) is IPlayerCharacter sourcePc)
                {
                    RegisterEntityMetadata(effect.SourceActorId, sourcePc);
                }

                var status = Service.DataManager.GetExcelSheet<Status>().GetRowOrDefault(effectId);

                combatEvents.AddEntry(targetId,
                    new CombatEvent.StatusEffect
                    {
                        Snapshot = snapshot,
                        TargetActorId = targetId,
                        Id = effectId,
                        StackCount = effect.StackCount <= status?.MaxStacks ? effect.StackCount : 0u,
                        Icon = status?.Icon,
                        Status = status?.Name.ExtractText(),
                        Description = status?.Description.ExtractText(),
                        Category = (StatusCategory)(status?.StatusCategory ?? 0),
                        SourceActorId = effect.SourceActorId,
                        Duration = effect.Duration
                    });
            }
        }
        catch (Exception e)
        {
            Service.PluginLog.Error(e, "Caught unexpected exception");
        }
    }

    public void CleanCombatEvents()
    {
        try
        {
            var entriesToRemove = new List<ulong>();
            foreach (var (id, events) in combatEvents)
            {
                if (events.Count == 0 || (DateTime.Now - events.Last().Snapshot.Time).TotalSeconds > plugin.Configuration.KeepCombatEventsForSeconds)
                {
                    entriesToRemove.Add(id);
                    continue;
                }

                var cutOffTime = DateTime.Now - TimeSpan.FromSeconds(plugin.Configuration.KeepCombatEventsForSeconds);
                for (var i = 0; i < events.Count; i++)
                    if (events[i].Snapshot.Time > cutOffTime)
                    {
                        events.RemoveRange(0, i);
                        break;
                    }
            }

            foreach (var entry in entriesToRemove)
                combatEvents.Remove(entry);

            entriesToRemove.Clear();

            foreach (var (id, death) in plugin.DeathsPerPlayer)
            {
                if (death.Count == 0 || (DateTime.Now - death.Last().TimeOfDeath).TotalMinutes > plugin.Configuration.KeepDeathsForMinutes)
                {
                    entriesToRemove.Add(id);
                    continue;
                }

                var cutOffTime = DateTime.Now - TimeSpan.FromMinutes(plugin.Configuration.KeepDeathsForMinutes);
                for (var i = 0; i < death.Count; i++)
                    if (death[i].TimeOfDeath > cutOffTime)
                    {
                        death.RemoveRange(0, i);
                        break;
                    }
            }

            foreach (var entry in entriesToRemove)
                plugin.DeathsPerPlayer.Remove(entry);
        }
        catch (Exception e)
        {
            Service.PluginLog.Error(e, "Error while clearing events");
        }
    }

    public void Dispose()
    {
        processPacketActionEffectHook.Dispose();
        processPacketEffectResultHook.Dispose();
        processPacketActorControlHook.Dispose();
    }
}