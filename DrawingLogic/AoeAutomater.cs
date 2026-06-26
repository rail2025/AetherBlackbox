using AetherBlackbox.Core;
using AetherBlackbox.Core.Mechanics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace AetherBlackbox.DrawingLogic
{
    public class ActiveAoe
    {
        public CustomMechanicEntry Template { get; set; }
        public Vector3 Origin { get; set; }
        public float Rotation { get; set; }
        public float ExpirationTime { get; set; }
    }

    public static class AoeAutomator
    {
        public static List<ActiveAoe> GetActiveAoEs(ReplayRecording recording, float currentTime, uint territoryId, PresetManager presetManager)
        {
            var activeAoEs = new List<ActiveAoe>();

            if (presetManager == null || recording == null || recording.Frames == null)
                return activeAoEs;

            float maxLookback = 15.0f;
            float windowStart = currentTime - maxLookback;
            var activeRules = presetManager.ActiveMemory;
            var registryMechanics = MechanicRegistry.GetMechanics(territoryId);
            var processedActions = new HashSet<(uint sourceId, uint actionId)>();

            for (int f = recording.Frames.Count - 1; f >= 0; f--)
            {
                var frame = recording.Frames[f];

                if (frame.TimeOffset > currentTime) continue;
                if (frame.TimeOffset < windowStart) break;

                for (int i = 0; i < frame.Ids.Count; i++)
                {
                    uint sourceId = frame.Ids[i];
                    uint actionId = 0;

                    if (frame.Actions != null && i < frame.Actions.Count && frame.Actions[i] != 0)
                        actionId = frame.Actions[i];
                    else if (frame.Casts != null && i < frame.Casts.Count && frame.Casts[i].ActionId != 0)
                        actionId = frame.Casts[i].ActionId;

                    if (actionId != 0)
                    {
                        if (!processedActions.Add((sourceId, actionId)))
                            continue;
                        // Exact: Action ID + Source Actor + Zone ID
                        var matchedRule = activeRules.FirstOrDefault(r => r.ActionId == actionId && r.SourceActorId == sourceId && r.ZoneId == territoryId);

                        // Scoped: Action ID + Zone ID
                        if (matchedRule == null)
                            matchedRule = activeRules.FirstOrDefault(r => r.ActionId == actionId && r.ZoneId == territoryId && r.SourceActorId == 0);

                        // Fuzzy: Action ID only
                        if (matchedRule == null)
                            matchedRule = activeRules.FirstOrDefault(r => r.ActionId == actionId && r.ZoneId == 0 && r.SourceActorId == 0);

                        var origin = new Vector3(frame.X[i], 0, frame.Z[i]);
                        var rotation = frame.Rot[i];

                        var targetId = frame.Targets[i];
                        if (targetId != 0 && targetId != sourceId && targetId != 0xE0000000)
                        {
                            int targetIdx = frame.Ids.IndexOf((uint)targetId);
                            if (targetIdx != -1)
                            {
                                var shape = matchedRule != null ? matchedRule.Shape : (registryMechanics != null && registryMechanics.TryGetValue(actionId, out var info) ? info.Shape : AoeShape.Circle);

                                if (shape == AoeShape.Circle || shape == AoeShape.Donut)
                                {
                                    origin = new Vector3(frame.X[targetIdx], 0, frame.Z[targetIdx]);
                                }
                                else if (shape == AoeShape.Cone || shape == AoeShape.Rect)
                                {
                                    var targetPos = new Vector3(frame.X[targetIdx], 0, frame.Z[targetIdx]);
                                    rotation = (float)Math.Atan2(targetPos.X - origin.X, targetPos.Z - origin.Z);
                                }
                            }
                        }

                        if (matchedRule != null)
                        {
                            float duration = matchedRule.Duration > 0 ? matchedRule.Duration : 1f;
                            float expTime = frame.TimeOffset + duration;

                            if (currentTime <= expTime)
                            {
                                activeAoEs.Add(new ActiveAoe
                                {
                                    Template = matchedRule,
                                    Origin = origin,
                                    Rotation = rotation,
                                    ExpirationTime = expTime
                                });
                            }
                        }
                        else if (registryMechanics != null && registryMechanics.TryGetValue(actionId, out var aoeInfo))
                        {
                            float duration = aoeInfo.Duration > 0 ? aoeInfo.Duration : 1f;
                            float expTime = frame.TimeOffset + duration;

                            if (currentTime <= expTime)
                            {
                                activeAoEs.Add(new ActiveAoe
                                {
                                    Template = new CustomMechanicEntry
                                    {
                                        ActionId = actionId,
                                        Shape = aoeInfo.Shape,
                                        Radius = aoeInfo.Radius,
                                        Width = aoeInfo.Width,
                                        InnerRadius = aoeInfo.InnerRadius,
                                        Angle = aoeInfo.Angle,
                                        Color = aoeInfo.Color,
                                        Thickness = aoeInfo.Thickness,
                                        IsFilled = aoeInfo.IsFilled,
                                        Duration = aoeInfo.Duration
                                    },
                                    Origin = origin,
                                    Rotation = rotation,
                                    ExpirationTime = expTime
                                });
                            }
                        }
                    }
                }
            }

            return activeAoEs;
        }
    }
}