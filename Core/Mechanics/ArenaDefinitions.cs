using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Interface.Textures.TextureWraps;

namespace AetherBlackbox.Core.Mechanics
{
    public enum TriggerType { Ability, StatusGain, StatusLoss, Custom }

    public sealed record ArenaPhase(string PhaseId, TriggerType TriggerType, IReadOnlyList<string> TriggerNames);

    public class ArenaVisual
    {
        public required string TexturePath { get; init; }
        public float Scale { get; init; } = 1.0f;
        public Vector2 Offset { get; init; } = Vector2.Zero;
        public bool AnchorToWaymarks { get; init; } = false;
    }

    public class ArenaDefinition
    {
        public uint TerritoryId { get; init; }
        public Dictionary<string, ArenaVisual> Visuals { get; init; } = new();
        public List<ArenaPhase> Phases { get; init; } = new();
    }

    public record PhaseTransition(float Time, string PhaseId);

    public class ArenaTimeline
    {
        private readonly List<PhaseTransition> _transitions = new();

        public void AddTransition(float time, string phaseId)
        {
            _transitions.Add(new PhaseTransition(time, phaseId));
        }

        public string Resolve(float currentTime)
        {
            if (_transitions.Count == 0) return string.Empty;

            for (int i = _transitions.Count - 1; i >= 0; --i)
            {
                if (_transitions[i].Time <= currentTime)
                    return _transitions[i].PhaseId;
            }
            return _transitions[0].PhaseId;
        }
    }
}