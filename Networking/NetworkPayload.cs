using System;

namespace AetherBlackbox.Networking
{
    public enum PayloadActionType : byte
    {
        AddObjects = 0,
        DeleteObjects = 1,
        UpdateObjects = 2,
        ClearPage = 3,
        UpdateGrid = 4,
        UpdateGridVisibility = 5,
        SessionLock = 6,
        Undo = 7,
        TimeSync = 8,
        EncounterSync = 9,
        BroadcastHeaders = 10,
    }

    [Serializable]
    public class NetworkPayload
    {
        public int PageIndex { get; set; }

        public PayloadActionType Action { get; set; }

        public byte[]? Data { get; set; }
    }
}
