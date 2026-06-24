using AetherBlackbox.Core;
using AetherBlackbox.Core.Mechanics;
using System.Collections.Generic;
using System.Numerics;

namespace AetherBlackbox.DrawingLogic
{
    public class ActiveAoe
    {
        public AoeInfo Info { get; set; }
        public Vector3 Origin { get; set; }
        public float Rotation { get; set; }
        public float ExpirationTime { get; set; }
    }

    public static class AoeAutomator
    {
        public static List<ActiveAoe> GetActiveAoEs(ReplayRecording recording, float currentTime, uint territoryId)
        {
            var activeAoEs = new List<ActiveAoe>();
            var mechanics = MechanicRegistry.GetMechanics(territoryId);

            if (mechanics == null || recording == null || recording.Frames == null)
                return activeAoEs;

            float maxLookback = 15.0f;
            float windowStart = currentTime - maxLookback;

            for (int f = recording.Frames.Count - 1; f >= 0; f--)
            {
                var frame = recording.Frames[f];

                if (frame.TimeOffset > currentTime) continue;
                if (frame.TimeOffset < windowStart) break;

                for (int i = 0; i < frame.Ids.Count; i++)
                {
                    uint actionId = frame.Actions[i];
                    if (actionId != 0 && mechanics.TryGetValue(actionId, out var aoeInfo))
                    {
                        float duration = aoeInfo.Duration > 0 ? aoeInfo.Duration : 0.5f;
                        float expTime = frame.TimeOffset + duration;

                        if (currentTime <= expTime)
                        {
                            activeAoEs.Add(new ActiveAoe
                            {
                                Info = aoeInfo,
                                Origin = new Vector3(frame.X[i], 0, frame.Z[i]),
                                Rotation = frame.Rot[i],
                                ExpirationTime = expTime
                            });
                        }
                    }
                }
            }

            return activeAoEs;
        }
    }
}