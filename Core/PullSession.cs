using System;
using System.Collections.Generic;
using AetherBlackbox.Events;

namespace AetherBlackbox.Core
{
    public class PullSession
    {
        public uint PullNumber { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public bool IsTruncated { get; set; }
        public string ZoneName { get; set; } = string.Empty;
        public ReplayRecording ReplayData { get; set; } = new();
        public List<Death> Deaths { get; set; } = new();
        public Dictionary<string, long> DamageByTarget { get; set; } = new();
        public TimeSpan Duration => (EndTime ?? StartTime) - StartTime;
        public string DisplayTitle => $"Pull #{PullNumber} ({StartTime:hh:mm tt}) - {Duration:mm\\:ss} {ZoneName}";
    }
}