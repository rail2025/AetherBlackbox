using System;

namespace AetherBlackbox.Networking
{
    public enum PayloadActionType : byte
    {
        AddObjects,
        DeleteObjects,
        UpdateObjects,
        SessionLock,
        Undo,
        TimeSync,
        EncounterSync,
        DrawLaser,
    }

    [Serializable]
    public class NetworkPayload
    {
        public int PageIndex { get; set; }

        public PayloadActionType Action { get; set; }

        public byte[]? Data { get; set; }
    }
}
