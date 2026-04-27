using AetherBlackbox.Events;
using System;

namespace AetherBlackbox.Windows
{
    internal static class InteractiveLogUtilities
    {
        public static bool IsDamagingEvent(CombatEvent evt)
        {
            return evt is CombatEvent.DamageTaken || evt is CombatEvent.DoT;
        }

        public static string GetSource(CombatEvent evt, Core.ReplayRecording? replayData)
        {
            uint sourceId = evt switch
            {
                CombatEvent.DamageTaken dt => dt.SourceActorId,
                CombatEvent.Healed h => h.SourceActorId,
                CombatEvent.StatusEffect s => s.SourceActorId,
                _ => 0
            };

            if (sourceId == 0) return "-";
            if (replayData?.Metadata != null && replayData.Metadata.TryGetValue(sourceId, out var meta))
                return meta.Name;
            return "Unknown";
        }

        public static string GetSearchableActionText(CombatEvent evt)
        {
            return evt switch
            {
                CombatEvent.DamageTaken dt => dt.Action,
                CombatEvent.Healed h => h.Action,
                CombatEvent.StatusEffect s => s.Status ?? string.Empty,
                _ => string.Empty
            };
        }

        public static bool MatchesSearch(CombatEvent evt, string search, Core.ReplayRecording? replayData)
        {
            if (string.IsNullOrWhiteSpace(search)) return true;

            var source = GetSource(evt, replayData);
            var action = GetSearchableActionText(evt);

            return source.Contains(search, StringComparison.OrdinalIgnoreCase)
                || action.Contains(search, StringComparison.OrdinalIgnoreCase);
        }
    }
}