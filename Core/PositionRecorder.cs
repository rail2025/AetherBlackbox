using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using static FFXIVClientStructs.FFXIV.Client.UI.Agent.AgentWKSMission;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Dalamud.Game.ClientState;

namespace AetherBlackbox.Core
{
    public struct ReplayStatus { public uint Id; public float Duration; public uint StackCount; public uint SourceId; }
    public struct ReplayCast { public uint ActionId; public float Current; public float Total; }
    public struct WaymarkSnapshot { public int ID; public float X; public float Z; public bool Active; }
    public enum EntityType { Player, Boss, Npc, Pet }
    public class SearchHeader
    {
        public int SchemaVersion { get; set; } = 1;
        public Dictionary<uint, string> AbilityManifest { get; set; } = new();
        public Dictionary<uint, uint> AbilityIconManifest { get; set; } = new();
        public Dictionary<uint, string> StatusManifest { get; set; } = new();
        public List<WaymarkSnapshot> WaymarkSnapshots { get; set; } = new();
        public List<string> DeathLog { get; set; } = new();
    }
    public struct EntityPositionSnapshot
    {
        public uint ObjectId;
        public string Name;
        public Vector3 Position;
        public float Rotation;
        public uint CurrentHp;
        public uint MaxHp;
        public uint ClassJobId;
        public DateTime Timestamp;
        public EntityType Type;
        public uint ModelId;
        public List<ReplayStatus>? Statuses;
        public ReplayCast Cast;
        public ulong TargetId;
        public uint LastLoggedActionId;
        public uint OwnerId;
    }
    public class ReplayRecording
    {
        public SearchHeader Header { get; set; } = new();
        public Dictionary<uint, ReplayMetadata> Metadata { get; set; } = new();
        public List<ReplayFrame> Frames { get; set; } = new();
        public List<WaymarkSnapshot> Waymarks { get; set; } = new();
    }

    public class ReplayMetadata
    {
        public uint EntityId { get; set; }
        public string Name { get; set; } = string.Empty;
        public uint MaxHp { get; set; }
        public uint ClassJobId { get; set; }
        public EntityType Type { get; set; }
        public uint ModelId { get; set; }
        public uint OwnerId { get; set; }
    }

    public class ReplayFrame
    {
        public float TimeOffset; // Time in seconds since start of replay
        public List<uint> Ids { get; set; } = new();
        public List<float> X { get; set; } = new(); // Only store X
        public List<float> Z { get; set; } = new(); // Only store Z (Depth)
        public List<float> Rot { get; set; } = new();
        public List<uint> Hp { get; set; } = new();
        public List<List<ReplayStatus>?> Statuses { get; set; } = new();
        public List<ReplayCast> Casts { get; set; } = new();
        public List<ulong> Targets { get; set; } = new();
        public List<uint> Actions { get; set; } = new();
    }

    public struct ReplayEntityState
    {
        public uint ObjectId;
        public Vector3 Position;
        public float Rotation;
        public uint CurrentHp;
        public DateTime DeathTime;
        public uint StatusHash;
    }
    public class PositionRecorder : IDisposable
    {
        private readonly Plugin plugin;
        private List<WaymarkSnapshot> initialWaymarks = new();
        private readonly Dictionary<uint, ReplayEntityState> lastRecordedStates = new();

        private readonly List<(DateTime Time, List<EntityPositionSnapshot> Data)> sessionData = new();
        private const int MaxRecordingSeconds = 1200;

        private readonly Dictionary<uint, uint> _actionBuffer = new();
        private readonly object _bufferLock = new();

        public bool IsRecording { get; private set; } = false;
        private DateTime sessionStartTime = DateTime.MinValue;
        private DateTime lastCaptureTime = DateTime.MinValue;
        private const double CaptureIntervalMs = 66.0; // ~15 FPS

        public PositionRecorder(Plugin plugin)
        {
            this.plugin = plugin;
            Service.Framework.Update += OnUpdate;
        }
        public void OnActionUsed(uint sourceId, uint actionId)
        {
            if (!IsRecording) return;
            lock (_bufferLock)
            {
                _actionBuffer[sourceId] = actionId;
            }
        }

        public unsafe void StartRecording()
        {
            lock (sessionData) sessionData.Clear();
            lock (_bufferLock) _actionBuffer.Clear();
            sessionStartTime = DateTime.Now;
            lastCaptureTime = DateTime.MinValue;
            IsRecording = true;
            initialWaymarks.Clear();
            lastRecordedStates.Clear();

            var controller = MarkingController.Instance();
            if (controller != null)
            {
                var markers = controller->FieldMarkers;

                // Iterate 0-7 (A, B, C, D, 1, 2, 3, 4)
                for (int i = 0; i < 8; i++)
                {
                    var marker = markers[i];

                    if (marker.Active)
                    {
                        initialWaymarks.Add(new WaymarkSnapshot
                        {
                            ID = i,
                            X = marker.Position.X,
                            Z = marker.Position.Z,
                            Active = true
                        });
                    }
                }
            }

            Service.PluginLog.Debug("[PositionRecorder] Recording started.");
        }

        public void StopRecording()
        {
            if (!IsRecording) return;
            IsRecording = false;
            Service.PluginLog.Debug($"[PositionRecorder] Recording stopped. Captured {sessionData.Count} frames.");
        }

        private void OnUpdate(IFramework framework)
        {
            if (!IsRecording || !Service.ClientState.IsLoggedIn) return;

            var snapshotTime = DateTime.Now;

            if ((snapshotTime - sessionStartTime).TotalSeconds > MaxRecordingSeconds)
            {
                Service.PluginLog.Warning("[PositionRecorder] Safety Cap Reached (20m). Force stopping.");
                StopRecording();
                if (plugin.PullManager?.CurrentSession != null) plugin.PullManager.CurrentSession.IsTruncated = true;
                return;
            }

            if ((snapshotTime - lastCaptureTime).TotalMilliseconds < CaptureIntervalMs) return;

            lastCaptureTime = snapshotTime;

            var frameData = new List<EntityPositionSnapshot>();

            if (plugin.PullManager?.CurrentSession != null && plugin.PullManager.CurrentSession.ReplayData.Header == null)
            {
                plugin.PullManager.CurrentSession.ReplayData.Header = new SearchHeader();
            }

            Dictionary<uint, uint> actionsThisFrame;
            lock (_bufferLock)
            {
                actionsThisFrame = new Dictionary<uint, uint>(_actionBuffer);
                _actionBuffer.Clear();
            }

            foreach (var obj in Service.ObjectTable)
            {
                uint currentAction = 0;
                bool isNewEntity = !lastRecordedStates.ContainsKey(obj.EntityId);
                
                bool shouldRecordMovement = true;
                bool shouldRecordAttributes = false;
                if (lastRecordedStates.TryGetValue(obj.EntityId, out var lastState))
                {
                    if (obj is IBattleChara battleObj)
                    {
                        if (battleObj.CurrentHp != lastState.CurrentHp) shouldRecordAttributes = true;
                    }

                    float dist = Vector3.Distance(obj.Position, lastState.Position);
                    float rotDiff = Math.Abs(obj.Rotation - lastState.Rotation);

                    // Only record if moved > 0.1 yalms or rotated >= 1 degree (~0.0174 rad)
                    if (dist <= 0.1f && rotDiff < 0.0174f)
                    {
                        shouldRecordMovement = false;
                    }
                }
                uint actionToLog = 0;
                if (actionsThisFrame.TryGetValue(obj.EntityId, out var actId))
                {
                    actionToLog = actId;
                }
                if (obj is IPlayerCharacter player)
                {
                    uint sHash = 0;
                    try
                    {
                        if (player.StatusList != null)
                        { 
                            unchecked { foreach (var s in player.StatusList) { if (s != null) sHash = (sHash * 397) ^ s.StatusId ^ s.Param ^ s.SourceId; }
                         }
                    }
                 } catch { }

                    bool statusChanged = lastState.ObjectId == 0 || sHash != lastRecordedStates[player.EntityId].StatusHash;

                    string playerName = string.Empty;
                    if (isNewEntity)
                    {
                        if (plugin.Configuration.AnonymizeNames)
                        {
                            playerName = player.ClassJob.RowId switch
                            {
                                19 => "PLD", 21 => "WAR", 32 => "DRK", 37 => "GNB",
                                24 => "WHM", 28 => "SCH", 33 => "AST", 40 => "SGE",
                                20 => "MNK", 22 => "DRG", 30 => "NIN", 34 => "SAM", 39 => "RPR", 41 => "VPR",
                                23 => "BRD", 31 => "MCH", 38 => "DNC",
                                25 => "BLM", 27 => "SMN", 35 => "RDM", 42 => "PCT",
                                _ => "PLAYER"
                            };
                        }
                        else
                        {
                            playerName = player.Name.TextValue;
                        }
                    }

                    var snapshot = new EntityPositionSnapshot
                    {
                        ObjectId = player.EntityId,
                        Name = playerName,
                        Position = player.Position,
                        Rotation = player.Rotation,
                        CurrentHp = player.CurrentHp,
                        MaxHp = player.MaxHp,
                        ClassJobId = player.ClassJob.RowId,
                        Timestamp = snapshotTime,
                        Type = EntityType.Player,
                        Statuses = statusChanged ? player.StatusList.Where(s => s != null).Select(s => new ReplayStatus { Id = s.StatusId, Duration = s.RemainingTime, StackCount = s.Param, SourceId = s.SourceId }).ToList() : null,
                        Cast = player.IsCasting ? new ReplayCast { ActionId = player.CastActionId, Current = player.CurrentCastTime, Total = player.TotalCastTime } : default,
                        TargetId = player.TargetObjectId,
                        LastLoggedActionId = actionToLog,
                        OwnerId = player.OwnerId
                    };

                    frameData.Add(snapshot);

                    lastRecordedStates[player.EntityId] = new ReplayEntityState
                    {
                        Position = player.Position,
                        Rotation = player.Rotation,
                        CurrentHp = player.CurrentHp,
                        ObjectId = 1,
                        StatusHash = sHash
                    };
                    if (actionToLog != 0)
                    {
                        Service.PluginLog.Debug($"[ActionTracker] Source: {player.Name}, Action: {actionToLog}");
                    }
                }
                else if (obj is IBattleNpc npc && npc.MaxHp > 0 && !string.IsNullOrEmpty(npc.Name.TextValue))
                {
                    bool isDead = npc.IsDead;
                    bool isActive = npc.IsCasting || actionToLog != 0 || shouldRecordMovement || isNewEntity || npc.IsTargetable;

                    if (lastRecordedStates.TryGetValue(npc.EntityId, out var state))
                    {
                        if (isActive && !isDead)
                        {
                            state.DeathTime = snapshotTime;
                            lastRecordedStates[npc.EntityId] = state;
                        }
                        else
                        {
                            if ((snapshotTime - state.DeathTime).TotalSeconds > 7.0) continue;
                        }
                    }
                    else
                    {
                        if (isDead) continue;
                        lastRecordedStates[npc.EntityId] = new ReplayEntityState { Position = npc.Position, Rotation = npc.Rotation, DeathTime = snapshotTime };
                    }

                    uint sHash = 0;
                    try
                    {
                        if (npc.StatusList != null) 
                        {
                            unchecked
                            {
                                foreach (var s in npc.StatusList)
                                {
                                    if (s != null) sHash = (sHash * 397) ^ s.StatusId ^ s.Param ^ s.SourceId;
                                }
                            }
                        } 
                    } catch { }
                    bool statusChanged = lastRecordedStates[npc.EntityId].ObjectId == 0 || sHash != lastRecordedStates[npc.EntityId].StatusHash;

                    var snapshot = new EntityPositionSnapshot
                    {
                        ObjectId = npc.EntityId,
                        Name = isNewEntity ? npc.Name.TextValue : string.Empty,
                        Position = npc.Position,
                        Rotation = npc.Rotation,
                        CurrentHp = npc.CurrentHp,
                        MaxHp = npc.MaxHp,
                        Timestamp = snapshotTime,
                        Type = (npc.OwnerId != 0 && npc.OwnerId != 0xE0000000) ? EntityType.Pet : (npc.IsTargetable ? EntityType.Boss : EntityType.Npc),
                        ModelId = (uint)npc.BaseId,
                        Statuses = statusChanged ? npc.StatusList.Where(s => s != null).Select(s => new ReplayStatus { Id = s.StatusId, Duration = s.RemainingTime, StackCount = s.Param, SourceId = s.SourceId }).ToList() : null,
                        Cast = npc.IsCasting ? new ReplayCast { ActionId = npc.CastActionId, Current = npc.CurrentCastTime, Total = npc.TotalCastTime } : default,
                        TargetId = npc.TargetObjectId,
                        LastLoggedActionId = actionToLog,
                        OwnerId = npc.OwnerId
                    };

                    frameData.Add(snapshot);

                    if (lastRecordedStates.TryGetValue(npc.EntityId, out var existingState))
                    {
                        existingState.Position = npc.Position;
                        existingState.Rotation = npc.Rotation;
                        existingState.CurrentHp = npc.CurrentHp;
                        existingState.ObjectId = 1;
                        existingState.StatusHash = sHash;
                        lastRecordedStates[npc.EntityId] = existingState;
                    }

                    if (actionToLog != 0)
                    {
                        Service.PluginLog.Debug($"[ActionTracker] Source: {npc.Name}, Action: {actionToLog}");
                    }
                }
            }

                lock (sessionData)
            {
                sessionData.Add((snapshotTime, frameData));
            }
        }

        public unsafe ReplayRecording GetReplayData()
        {
            lock (sessionData)
            {
                var recording = new ReplayRecording();
                var controller = MarkingController.Instance();
                if (controller != null)
                {
                    for (int i = 0; i < 8; i++)
                    {
                        var marker = controller->FieldMarkers[i];
                        if (marker.Active)
                        {
                            recording.Waymarks.Add(new WaymarkSnapshot
                            {
                                ID = i,
                                X = marker.Position.X,
                                Z = marker.Position.Z,
                                Active = true
                            });
                        }
                    }
                }

                if (sessionData.Count == 0) return recording;
                var startTime = sessionData[0].Time;
                foreach (var (time, snapshots) in sessionData)
                {
                    var frame = new ReplayFrame
                    {
                        TimeOffset = (float)(time - startTime).TotalSeconds
                    };

                    foreach (var entity in snapshots)
                    {
                        if (!recording.Metadata.ContainsKey(entity.ObjectId) && !string.IsNullOrEmpty(entity.Name))
                        {
                            recording.Metadata[entity.ObjectId] = new ReplayMetadata
                            {
                                Name = entity.Name,
                                MaxHp = entity.MaxHp,
                                ClassJobId = entity.ClassJobId,
                                Type = entity.Type,
                                ModelId = entity.ModelId,
                                OwnerId = entity.OwnerId
                            };
                        }

                        if (entity.LastLoggedActionId != 0 && !recording.Header.AbilityManifest.ContainsKey(entity.LastLoggedActionId))
                        {
                            var action = Service.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Action>()?.GetRowOrDefault(entity.LastLoggedActionId);
                            if (action.HasValue)
                            {
                                recording.Header.AbilityManifest.Add(entity.LastLoggedActionId, action.Value.Name.ToString());
                                recording.Header.AbilityIconManifest.Add(entity.LastLoggedActionId, action.Value.Icon);
                            }
                        }
                        if (entity.Cast.ActionId != 0 && !recording.Header.AbilityManifest.ContainsKey(entity.Cast.ActionId))
                        {
                            var castAction = Service.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Action>()?.GetRowOrDefault(entity.Cast.ActionId);
                            if (castAction.HasValue)
                            {
                                recording.Header.AbilityManifest.Add(entity.Cast.ActionId, castAction.Value.Name.ToString());
                                recording.Header.AbilityIconManifest.Add(entity.Cast.ActionId, castAction.Value.Icon);
                            }
                        }
                        if (entity.Statuses != null)
                        {
                            foreach (var s in entity.Statuses)
                            {
                                if (!recording.Header.StatusManifest.ContainsKey(s.Id))
                                {
                                    var status = Service.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Status>()?.GetRowOrDefault(s.Id);
                                    if (status.HasValue)
                                        recording.Header.StatusManifest.Add(s.Id, status.Value.Name.ToString());
                                }
                            }
                        }

                        frame.Ids.Add(entity.ObjectId);
                        frame.X.Add((float)Math.Round(entity.Position.X, 2));
                        frame.Z.Add((float)Math.Round(entity.Position.Z, 2));
                        frame.Rot.Add((float)Math.Round(entity.Rotation, 2));
                        frame.Hp.Add(entity.CurrentHp);
                        frame.Statuses.Add(entity.Statuses);
                        frame.Casts.Add(entity.Cast);
                        frame.Targets.Add(entity.TargetId);
                        frame.Actions.Add(entity.LastLoggedActionId);
                    }
                    recording.Frames.Add(frame);
                }
                Service.PluginLog.Debug($"[SchemaCheck] Replay Ready. Abilities: {recording.Header.AbilityManifest.Count}, Statuses: {recording.Header.StatusManifest.Count}, Metadata: {recording.Metadata.Count}");

                return recording;
            }
        }

        public void Dispose()
        {
            Service.Framework.Update -= OnUpdate;
            sessionData.Clear();
        }

    }
}