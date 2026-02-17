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

public class CombatEventCapture : IDisposable {
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

    private unsafe void ProcessPacketActionEffectDetour(
        uint casterEntityId, Character* casterPtr, Vector3* targetPos, ActionEffectHandler.Header* effectHeader, ActionEffectHandler.TargetEffects* effectArray,
        GameObjectId* targetEntityIds) {
        processPacketActionEffectHook.Original(casterEntityId, casterPtr, targetPos, effectHeader, effectArray, targetEntityIds);

        try {
            if (effectHeader->NumTargets == 0)
                return;

            var actionId = (ActionType)effectHeader->ActionType switch {
                ActionType.Mount => 0xD000000 + effectHeader->ActionId,
                ActionType.Item => 0x2000000 + effectHeader->ActionId,
                _ => effectHeader->SpellId
            };

            if (plugin.ConditionEvaluator.ShouldCapture(casterEntityId))
            {
                plugin.PositionRecorder.OnActionUsed(casterEntityId, actionId);
            }

            Action? action = null;
            string? source = null;
            List<uint>? additionalStatus = null;

            for (var i = 0; i < effectHeader->NumTargets; i++) {
                var actionTargetId = (uint)(targetEntityIds[i] & uint.MaxValue);
                if (!plugin.ConditionEvaluator.ShouldCapture(actionTargetId))
                    continue;
                var targetObj = Service.ObjectTable.SearchById(actionTargetId);
                if (plugin.PullManager.IsInSession && targetObj is IBattleNpc npc)
                {
                    for (var k = 0; k < 8; k++)
                    {
                        ref var eff = ref effectArray[i].Effects[k];
                        if ((ActionEffectType)eff.Type is ActionEffectType.Damage or ActionEffectType.BlockedDamage or ActionEffectType.ParriedDamage)
                        {
                            uint dmg = eff.Value;
                            if ((eff.Param4 & 0x40) == 0x40) dmg += (uint)eff.Param3 << 16;

                            var npcName = npc.Name.TextValue;
                            if (!string.IsNullOrEmpty(npcName))
                            {
                                if (!plugin.PullManager.CurrentSession!.DamageByTarget.ContainsKey(npcName))
                                    plugin.PullManager.CurrentSession.DamageByTarget[npcName] = 0;
                                plugin.PullManager.CurrentSession.DamageByTarget[npcName] += dmg;
                            }
                        }
                    }
                }
                if (Service.ObjectTable.SearchById(actionTargetId) is not IPlayerCharacter p)
                    continue;
                for (var j = 0; j < 8; j++) {
                    ref var actionEffect = ref effectArray[i].Effects[j];
                    if (actionEffect.Type == 0)
                        continue;
                    uint amount = actionEffect.Value;
                    if ((actionEffect.Param4 & 0x40) == 0x40)
                        amount += (uint)actionEffect.Param3 << 16;

                    action ??= Service.DataManager.GetExcelSheet<Action>().GetRowOrDefault(actionId);
                    source ??= casterPtr->NameString;

                    switch ((ActionEffectType)actionEffect.Type) {
                        case ActionEffectType.Miss:
                        case ActionEffectType.Damage:
                        case ActionEffectType.BlockedDamage:
                        case ActionEffectType.ParriedDamage:
                            if (additionalStatus == null) {
                                var statusManager = casterPtr->GetStatusManager();
                                additionalStatus = [];
                                if (statusManager != null) {
                                    foreach (ref var status in statusManager->Status) {
                                        if (status.StatusId is 1203 or 1195 or 1193 or 860 or 1715 or 2115 or 3642)
                                            additionalStatus.Add(status.StatusId);
                                    }
                                }
                            }

                            combatEvents.AddEntry(actionTargetId,
                                new CombatEvent.DamageTaken {
                                    // 1203 = Addle
                                    // 1195 = Feint
                                    // 1193 = Reprisal
                                    //  860 = Dismantled
                                    // 1715 = Malodorous, BLU Bad Breath
                                    // 2115 = Conked, BLU Magic Hammer
                                    // 3642 = Candy Cane, BLU Candy Cane
                                    Snapshot = p.Snapshot(true, additionalStatus),
                                    Source = source,
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
                            combatEvents.AddEntry(actionTargetId,
                                new CombatEvent.Healed {
                                    Snapshot = p.Snapshot(true),
                                    Source = source,
                                    Amount = amount,
                                    Action = action?.Name.ExtractText() ?? "",
                                    Icon = action?.Icon,
                                    Crit = (actionEffect.Param1 & 0x20) == 0x20
                                });
                            break;
                    }
                }
            }
        } catch (Exception e) {
            Service.PluginLog.Error(e, "Caught unexpected exception");
        }
    }

    private void ProcessPacketActorControlDetour(
        uint entityId, uint category, uint param1, uint param2, uint param3, uint param4, uint param5, uint param6, uint param7, uint param8, GameObjectId targetId,
        bool param9)
    {
        processPacketActorControlHook.Original(entityId, category, param1, param2, param3, param4, param5, param6, param7, param8, targetId, param9);

        try {
            if (!plugin.ConditionEvaluator.ShouldCapture(entityId))
                return;

            if (Service.ObjectTable.SearchById(entityId) is not IPlayerCharacter p)
                return;

            switch ((ActorControlCategory)category) {
                case ActorControlCategory.DoT: combatEvents.AddEntry(entityId, new CombatEvent.DoT { Snapshot = p.Snapshot(), Amount = param2 }); break;
                case ActorControlCategory.HoT:
                    if (param1 != 0) {
                        var sourceName = Service.ObjectTable.SearchById(entityId)?.Name.TextValue;
                        var status = Service.DataManager.GetExcelSheet<Status>().GetRowOrDefault(param1);
                        combatEvents.AddEntry(entityId,
                            new CombatEvent.Healed {
                                Snapshot = p.Snapshot(),
                                Source = sourceName,
                                Amount = param2,
                                Action = status?.Name.ExtractText() ?? "",
                                Icon = status?.Icon,
                                Crit = param4 == 1
                            });
                    } else {
                        combatEvents.AddEntry(entityId, new CombatEvent.HoT { Snapshot = p.Snapshot(), Amount = param2 });
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
                                PlayerName = p.Name.TextValue,
                                TimeOfDeath = DateTime.Now,
                                Events = events,
                                ReplayData = replayData,
                                TerritoryTypeId = Service.ClientState.TerritoryType
                            };

                            Service.PluginLog.Info($"Capture: Created Death object for {death.PlayerName} at {death.TimeOfDeath:HH:mm:ss.fff}");

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
        } catch (Exception e) {
            Service.PluginLog.Error(e, "Caught unexpected exception");
        }
    }

    private unsafe void ProcessPacketEffectResultDetour(uint targetId, IntPtr actionIntegrityData, byte isReplay) {
        processPacketEffectResultHook.Original(targetId, actionIntegrityData, isReplay);

        try {
            var message = (AddStatusEffect*)actionIntegrityData;
            if (!plugin.ConditionEvaluator.ShouldCapture(targetId))
                return;

            if (Service.ObjectTable.SearchById(targetId) is not IPlayerCharacter p)
                return;

            var effects = (StatusEffectAddEntry*)message->Effects;
            var effectCount = Math.Min(message->EffectCount, 4u);
            string? cachedSource = null;
            uint lastSourceId = 0;

            for (uint j = 0; j < effectCount; j++)
            {
                var effect = effects[j];
                var effectId = effect.EffectId;
                if (effectId <= 0 || effect.Duration < 0)
                    continue;

                if (cachedSource == null || effect.SourceActorId != lastSourceId)
                {
                    cachedSource = Service.ObjectTable.SearchById(effect.SourceActorId)?.Name.TextValue;
                    lastSourceId = effect.SourceActorId;
                }

                var status = Service.DataManager.GetExcelSheet<Status>().GetRowOrDefault(effectId);

                combatEvents.AddEntry(targetId,
                    new CombatEvent.StatusEffect {
                        Snapshot = p.Snapshot(),
                        Id = effectId,
                        StackCount = effect.StackCount <= status?.MaxStacks ? effect.StackCount : 0u,
                        Icon = status?.Icon,
                        Status = status?.Name.ExtractText(),
                        Description = status?.Description.ExtractText(),
                        Category = (StatusCategory)(status?.StatusCategory ?? 0),
                        Source = cachedSource,
                        Duration = effect.Duration
                    });
            }
        } catch (Exception e) {
            Service.PluginLog.Error(e, "Caught unexpected exception");
        }
    }

    public void CleanCombatEvents() {
        try {
            var entriesToRemove = new List<ulong>();
            foreach (var (id, events) in combatEvents) {
                if (events.Count == 0 || (DateTime.Now - events.Last().Snapshot.Time).TotalSeconds > plugin.Configuration.KeepCombatEventsForSeconds) {
                    entriesToRemove.Add(id);
                    continue;
                }

                var cutOffTime = DateTime.Now - TimeSpan.FromSeconds(plugin.Configuration.KeepCombatEventsForSeconds);
                for (var i = 0; i < events.Count; i++)
                    if (events[i].Snapshot.Time > cutOffTime) {
                        events.RemoveRange(0, i);
                        break;
                    }
            }

            foreach (var entry in entriesToRemove)
                combatEvents.Remove(entry);

            entriesToRemove.Clear();

            foreach (var (id, death) in plugin.DeathsPerPlayer) {
                if (death.Count == 0 || (DateTime.Now - death.Last().TimeOfDeath).TotalMinutes > plugin.Configuration.KeepDeathsForMinutes) {
                    entriesToRemove.Add(id);
                    continue;
                }

                var cutOffTime = DateTime.Now - TimeSpan.FromMinutes(plugin.Configuration.KeepDeathsForMinutes);
                for (var i = 0; i < death.Count; i++)
                    if (death[i].TimeOfDeath > cutOffTime) {
                        death.RemoveRange(0, i);
                        break;
                    }
            }

            foreach (var entry in entriesToRemove)
                plugin.DeathsPerPlayer.Remove(entry);
        } catch (Exception e) {
            Service.PluginLog.Error(e, "Error while clearing events");
        }
    }

    public void Dispose() {
        processPacketActionEffectHook.Dispose();
        processPacketEffectResultHook.Dispose();
        processPacketActorControlHook.Dispose();
    }
}
