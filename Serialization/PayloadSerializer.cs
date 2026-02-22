using System;
using System.IO;
using System.Collections.Generic;
using System.Numerics;
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
        public static byte[] SerializeTimeSync(float timeOffset)
        {
            using (var memoryStream = new MemoryStream())
            using (var writer = new BinaryWriter(memoryStream))
            {
                writer.Write(timeOffset);
                return memoryStream.ToArray();
            }
        }

        public static float DeserializeTimeSync(byte[] data)
        {
            using (var memoryStream = new MemoryStream(data))
            using (var reader = new BinaryReader(memoryStream))
            {
                return reader.ReadSingle();
            }
        }

        public static byte[] SerializeEncounterSync(uint territoryType, int pullNumber, ulong activeDeathId)
        {
            using (var memoryStream = new MemoryStream())
            using (var writer = new BinaryWriter(memoryStream))
            {
                writer.Write(territoryType);
                writer.Write(pullNumber);
                writer.Write(activeDeathId);
                return memoryStream.ToArray();
            }
        }

        public static (uint territoryType, int pullNumber, ulong activeDeathId) DeserializeEncounterSync(byte[] data)
        {
            using (var memoryStream = new MemoryStream(data))
            using (var reader = new BinaryReader(memoryStream))
            {
                return (reader.ReadUInt32(), reader.ReadInt32(), reader.ReadUInt64());
            }
        }

        public static byte[] SerializeDrawLaser(List<Vector2> points, Vector4 color)
        {
            using (var memoryStream = new MemoryStream())
            using (var writer = new BinaryWriter(memoryStream))
            {
                writer.Write(color.X);
                writer.Write(color.Y);
                writer.Write(color.Z);
                writer.Write(color.W);

                writer.Write(points.Count);
                foreach (var point in points)
                {
                    writer.Write(point.X);
                    writer.Write(point.Y);
                }
                return memoryStream.ToArray();
            }
        }

        public static (List<Vector2> points, Vector4 color) DeserializeDrawLaser(byte[] data)
        {
            using (var memoryStream = new MemoryStream(data))
            using (var reader = new BinaryReader(memoryStream))
            {
                var color = new Vector4(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                int count = reader.ReadInt32();
                var points = new List<Vector2>(count);
                for (int i = 0; i < count; i++)
                {
                    points.Add(new Vector2(reader.ReadSingle(), reader.ReadSingle()));
                }
                return (points, color);
            }
        }
    }
}