using System.Collections.Generic;
using AetherBlackbox.Core.Mechanics;

namespace AetherBlackbox.Core
{
    public static class MechanicRegistry
    {
        public static readonly Dictionary<uint, Dictionary<uint, AoeInfo>> TerritoryMap = new()
        {
            { 1363, UMAD.Abilities }
        };

        public static Dictionary<uint, AoeInfo>? GetMechanics(uint territoryId)
        {
            if (TerritoryMap.TryGetValue(territoryId, out var mechanics))
            {
                return mechanics;
            }

            return null;
        }
    }
}