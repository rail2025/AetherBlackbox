    using System;
    using System.Collections.Generic;
    using AetherBlackbox.Events;

    namespace AetherBlackbox.Core
    {
    public enum SessionLoadState { Queued, Loading, Loaded, Failed }
    public class PullSession
        {
            public Guid SessionId { get; set; } = Guid.NewGuid();
            public SessionLoadState LoadState { get; set; } = SessionLoadState.Loaded;
            public string ProgressText { get; set; } = string.Empty;
            public string ErrorMessage { get; set; } = string.Empty;

            public uint PullNumber { get; set; }
            public DateTime StartTime { get; set; }
            public DateTime? EndTime { get; set; }
            public bool IsTruncated { get; set; }
            public string ZoneName { get; set; } = string.Empty;
            public ReplayRecording ReplayData { get; set; } = new();
            public List<Death> Deaths { get; set; } = new();
            public Dictionary<string, long> DamageByTarget { get; set; } = new();
            public List<CombatEvent> DetailedDamageEvents { get; set; } = new();
            public Dictionary<uint, ReplayMetadata> Metadata { get; set; } = new();
            public TimeSpan Duration => (EndTime ?? StartTime) - StartTime;
            public string DisplayTitle => $"Pull #{PullNumber} ({StartTime:hh:mm tt}) - {Duration:mm\\:ss} {ZoneName}";
            public string IdentityHash => $"{ZoneName}_{StartTime.Ticks}";
            public int EventCount => (ReplayData?.Frames?.Count ?? 0) + Deaths.Count;
        }
    }