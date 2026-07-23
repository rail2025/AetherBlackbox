using Lumina.Excel.Sheets;
using System.Collections.Generic;

namespace AetherBlackbox.Events
{
    public static class GameDataCache
    {
        private static readonly Dictionary<uint, Action?> ActionCache = new();
        private static readonly Dictionary<uint, Status?> StatusCache = new();

        public static Action? GetAction(uint id)
        {
            if (ActionCache.TryGetValue(id, out var action)) return action;
            action = Service.DataManager.GetExcelSheet<Action>()?.GetRowOrDefault(id);
            ActionCache[id] = action;
            return action;
        }

        public static Status? GetStatus(uint id)
        {
            if (StatusCache.TryGetValue(id, out var status)) return status;
            status = Service.DataManager.GetExcelSheet<Status>()?.GetRowOrDefault(id);
            StatusCache[id] = status;
            return status;
        }
    }
}