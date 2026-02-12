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
    public struct ReplayStatus { public uint Id; public float Duration; public uint StackCount; }
    public struct ReplayCast { public uint ActionId; public float Current; public float Total; }
    public struct WaymarkSnapshot { public int ID; public float X; public float Z; public bool Active; }
    public enum EntityType { Player, Boss }
    public class SearchHeader
    {
        public int SchemaVersion { get; set; } = 1;
        public Dictionary<uint, string> AbilityManifest { get; set; } = new();
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
        public List<ReplayStatus> Statuses;
        public ReplayCast Cast;
        public ulong TargetId;
        public uint LastLoggedActionId;
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
        public string Name { get; set; } = string.Empty;
        public uint MaxHp { get; set; }
        public uint ClassJobId { get; set; }
        public EntityType Type { get; set; }
        public uint ModelId { get; set; }
    }

    public class ReplayFrame
    {
        public float TimeOffset; // Time in seconds since start of replay
        public List<uint> Ids { get; set; } = new();
        public List<float> X { get; set; } = new(); // Only store X
        public List<float> Z { get; set; } = new(); // Only store Z (Depth)
        public List<float> Rot { get; set; } = new();
        public List<uint> Hp { get; set; } = new();
        public List<List<ReplayStatus>> Statuses { get; set; } = new();
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
                
                bool isNewEntity = false;
                if (plugin.PullManager?.CurrentSession != null)
                {
                    var metadata = plugin.PullManager.CurrentSession.ReplayData.Metadata;
                    if (!metadata.ContainsKey(obj.EntityId))
                    {
                        isNewEntity = true;
                    }
                }
                
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
                    bool statusChanged = lastState.ObjectId == 0 || player.StatusList.Length != lastRecordedStates[player.EntityId].ObjectId;

                    if (!shouldRecordMovement && !shouldRecordAttributes && !statusChanged && !isNewEntity && !player.IsCasting && actionToLog == 0)
                        continue;
                    var snapshot = new EntityPositionSnapshot
                    {
                        ObjectId = player.EntityId,
                        Name = isNewEntity ? player.Name.TextValue : string.Empty,
                        Position = player.Position,
                        Rotation = player.Rotation,
                        CurrentHp = player.CurrentHp,
                        MaxHp = player.MaxHp,
                        ClassJobId = player.ClassJob.RowId,
                        Timestamp = snapshotTime,
                        Type = EntityType.Player,
                        Statuses = player.StatusList.Select(s => new ReplayStatus { Id = s.StatusId, Duration = s.RemainingTime, StackCount = s.Param }).ToList(),
                        Cast = player.IsCasting ? new ReplayCast { ActionId = player.CastActionId, Current = player.CurrentCastTime, Total = player.TotalCastTime } : default,
                        TargetId = player.TargetObjectId,
                        LastLoggedActionId = actionToLog
                    };

                    frameData.Add(snapshot);
                    
                    lastRecordedStates[player.EntityId] = new ReplayEntityState
                    {
                        Position = player.Position,
                        Rotation = player.Rotation,
                        CurrentHp = player.CurrentHp,
                        ObjectId = (uint)player.StatusList.Length
                    };
                    if (actionToLog != 0)
                    {
                        Service.PluginLog.Debug($"[ActionTracker] Source: {player.Name}, Action: {actionToLog}");
                    }
                }
                else if (obj is IBattleNpc npc && npc.MaxHp > 0 && npc.IsTargetable && !string.IsNullOrEmpty(npc.Name.TextValue))
                {
                    if (!shouldRecordMovement && !isNewEntity && !npc.IsCasting && actionToLog == 0)
                        continue;

                    var snapshot = new EntityPositionSnapshot
                    {
                        ObjectId = npc.EntityId,
                        Name = isNewEntity ? npc.Name.TextValue : string.Empty,
                        Position = npc.Position,
                        Rotation = npc.Rotation,
                        CurrentHp = npc.CurrentHp,
                        MaxHp = npc.MaxHp,
                        Timestamp = snapshotTime,
                        Type = EntityType.Boss,
                        ModelId = (uint)npc.BaseId,
                        Statuses = npc.StatusList.Select(s => new ReplayStatus { Id = s.StatusId, Duration = s.RemainingTime, StackCount = s.Param }).ToList(),
                        Cast = npc.IsCasting ? new ReplayCast { ActionId = npc.CastActionId, Current = npc.CurrentCastTime, Total = npc.TotalCastTime } : default,
                        TargetId = npc.TargetObjectId,
                        LastLoggedActionId = actionToLog
                    };

                    frameData.Add(snapshot);
                    lastRecordedStates[npc.EntityId] = new ReplayEntityState { Position = npc.Position, Rotation = npc.Rotation };

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
                                ModelId = entity.ModelId
                            };
                        }
                       
                        if (entity.LastLoggedActionId != 0 && !recording.Header.AbilityManifest.ContainsKey(entity.LastLoggedActionId))
                        {
                            var action = Service.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Action>()?.GetRowOrDefault(entity.LastLoggedActionId);
                            if (action != null)
                                recording.Header.AbilityManifest.Add(entity.LastLoggedActionId, action.Value.Name.ToString());
                        }

                        if (entity.Statuses != null)
                        {
                            foreach (var s in entity.Statuses)
                            {
                                if (!recording.Header.StatusManifest.ContainsKey(s.Id))
                                {
                                    var status = Service.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Status>()?.GetRowOrDefault(s.Id);
                                    if (status != null)
                                        recording.Header.StatusManifest.Add(s.Id, status.Value.Name.ToString());
                                }
                            }
                        }

                        frame.Ids.Add(entity.ObjectId);
                        frame.X.Add((float)Math.Round(entity.Position.X, 2));
                        frame.Z.Add((float)Math.Round(entity.Position.Z, 2));
                        frame.Rot.Add((float)Math.Round(entity.Rotation, 2));
                        frame.Hp.Add(entity.CurrentHp);
                        frame.Statuses.Add(entity.Statuses ?? new());
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