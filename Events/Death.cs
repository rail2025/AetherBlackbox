using System;
using System.Collections.Generic;
using AetherBlackbox.Core;

namespace AetherBlackbox.Events;

public record Death
{
    public uint PlayerId { get; init; }
    public string PlayerName { get; init; } = null!;
    public DateTime TimeOfDeath { get; init; }
    public uint TerritoryTypeId { get; init; }

    public List<CombatEvent> Events { get; init; } = null!;

    public ReplayRecording ReplayData { get; init; } = new();

    public string Title
    {
        get
        {
            var timeSpan = DateTime.Now.Subtract(TimeOfDeath);

            if (timeSpan <= TimeSpan.FromSeconds(60))
                return $"{timeSpan.Seconds} seconds ago";

            if (timeSpan <= TimeSpan.FromMinutes(60))
                return timeSpan.Minutes > 1 ? $"{timeSpan.Minutes} minutes ago" : "about a minute ago";

            return timeSpan.Hours > 1 ? $"{timeSpan.Hours} hours ago" : "about an hour ago";
        }
    }
}