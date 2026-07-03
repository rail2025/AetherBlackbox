using System.Collections.Generic;
using AetherBlackbox.Core.Mechanics;

namespace AetherBlackbox.Core
{
    public static class MechanicRegistry
    {
        public static readonly Dictionary<uint, Dictionary<uint, AoeInfo>> TerritoryMap = new()
        {
            //{ 1363, UMAD.Abilities },
            //{ 1323, M10s.Abilities },
            //{ 1327, M12sP1.Abilities }
        };

        public static Dictionary<uint, AoeInfo>? GetMechanics(uint territoryId)
        {
            var result = new Dictionary<uint, AoeInfo>();

            if (TerritoryMap.TryGetValue(territoryId, out var defaultMechanics))
            {
                foreach (var item in defaultMechanics)
                {
                    result[item.Key] = item.Value;
                }
            }

            var customEntries = MechanicDatabaseManager.LoadTerritory(territoryId);
            if (customEntries != null)
            {
                foreach (var entry in customEntries)
                {
                    result[entry.ActionId] = new AoeInfo
                    {
                        Shape = entry.Shape,
                        Radius = entry.Radius,
                        Width = entry.Width,
                        InnerRadius = entry.InnerRadius,
                        Angle = entry.Angle,
                        Color = entry.Color,
                        Thickness = entry.Thickness,
                        IsFilled = entry.IsFilled,
                        Duration = entry.Duration
                    };
                }
            }

            return result.Count > 0 ? result : null;
        }
    }
}