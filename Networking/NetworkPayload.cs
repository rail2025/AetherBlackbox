using System;

namespace AetherBlackbox.Networking
{
    public enum PayloadActionType : byte
    {
        AddObjects,
        DeleteObjects,
        UpdateObjects,
        ClearPage,
        ReplacePage,
        AddNewPage,
        DeletePage,
        UpdateGrid,
        UpdateGridVisibility,
        MovePage,
        SessionLock,
        Undo,
    }

    [Serializable]
    public class NetworkPayload
    {
        public int PageIndex { get; set; }

        public PayloadActionType Action { get; set; }

        public byte[]? Data { get; set; }
    }
}
