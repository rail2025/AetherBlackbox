using System;
using System.Collections.Generic;
using System.Numerics;

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
    public struct ReplayAoeEvent
    {
        public float TimeOffset;
        public uint ActionId;
        public uint SourceId;
        public Vector3 Origin;
        public float Rotation;
    }
    public struct EntityPositionSnapshot
    {
        public uint ObjectId;
        public string Name;
        public string TeamTag;
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
        public AetherBlackbox.Core.Mechanics.ArenaTimeline ArenaTimeline { get; set; } = new();
        public Dictionary<uint, ReplayMetadata> Metadata { get; set; } = new();
        public List<ReplayFrame> Frames { get; set; } = new();
        public List<WaymarkSnapshot> Waymarks { get; set; } = new();
        public List<ReplayAoeEvent> AoeEvents { get; set; } = new();
    }

    public class ReplayMetadata
    {
        public uint EntityId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string TeamTag { get; set; } = string.Empty;
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
}