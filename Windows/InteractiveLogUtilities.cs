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

        public static string GetSource(CombatEvent evt)
        {
            return evt switch
            {
                CombatEvent.DamageTaken dt => dt.Source ?? "-",
                CombatEvent.Healed h => h.Source ?? "-",
                CombatEvent.StatusEffect s => s.Source ?? "-",
                _ => "-"
            };
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

        public static bool MatchesSearch(CombatEvent evt, string search)
        {
            if (string.IsNullOrWhiteSpace(search)) return true;

            var source = GetSource(evt);
            var action = GetSearchableActionText(evt);

            return source.Contains(search, StringComparison.OrdinalIgnoreCase)
                || action.Contains(search, StringComparison.OrdinalIgnoreCase);
        }
    }
}
