using System.IO;
using UnityEngine;

// Server <-> Client
public class DetonateGrenadeMessage : INetworkMessage
{
    public uint Id;
    public Vector3 Position;
    public GrenadeType Type;

    public NetworkMessageType GetMessageType()
    {
        return NetworkMessageType.DetonateGrenade;
    }

    public void Serialize(BinaryWriter writer)
    {
        writer.Write(Id);
        NetworkSerializationUtils.Serialize(writer, Position);
        writer.Write((byte)Type);
    }
    public void Deserialize(BinaryReader reader)
    {
        Id = reader.ReadUInt32();
        NetworkSerializationUtils.Deserialize(reader, ref Position);
        Type = (GrenadeType)reader.ReadByte();
    }
}