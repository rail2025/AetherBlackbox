using System;
using System.IO;
using AetherBlackbox.Networking;

namespace AetherBlackbox.Serialization
{
    public static class PayloadSerializer
    {
        public static byte[] Serialize(NetworkPayload payload)
        {
            if (payload == null)
                throw new ArgumentNullException(nameof(payload));

            using (var memoryStream = new MemoryStream())
            using (var writer = new BinaryWriter(memoryStream))
            {
                writer.Write(payload.PageIndex);
                writer.Write((byte)payload.Action);

                if (payload.Data != null && payload.Data.Length > 0)
                {
                    writer.Write(payload.Data.Length);
                    writer.Write(payload.Data);
                }
                else
                {
                    writer.Write(0);
                }

                return memoryStream.ToArray();
            }
        }

        public static NetworkPayload? Deserialize(byte[] data)
        {
            if (data == null || data.Length == 0)
                return null;

            try
            {
                using (var memoryStream = new MemoryStream(data))
                using (var reader = new BinaryReader(memoryStream))
                {
                    var payload = new NetworkPayload();

                    if (reader.BaseStream.Position + sizeof(int) > reader.BaseStream.Length) return null;
                    payload.PageIndex = reader.ReadInt32();

                    if (reader.BaseStream.Position + sizeof(byte) > reader.BaseStream.Length) return null;
                    payload.Action = (PayloadActionType)reader.ReadByte();

                    if (reader.BaseStream.Position + sizeof(int) > reader.BaseStream.Length) return null;
                    int dataLength = reader.ReadInt32();

                    if (dataLength > 0)
                    {
                        if (reader.BaseStream.Position + dataLength > reader.BaseStream.Length) return null;
                        payload.Data = reader.ReadBytes(dataLength);
                    }
                    else
                    {
                        payload.Data = null;
                    }

                    return payload;
                }
            }
            catch (Exception ex)
            {
                Service.PluginLog?.Error(ex, "Failed to deserialize NetworkPayload.");
                return null;
            }
        }
    }
}
