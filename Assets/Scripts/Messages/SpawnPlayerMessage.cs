using System.IO;
using UnityEngine;

// Server -> Client
public class SpawnPlayerMessage : INetworkMessage
{
    public uint PlayerId;
    public Vector3 PlayerPosition;
    public float PlayerYAngle;

    public NetworkMessageType GetMessageType()
    {
        return NetworkMessageType.SpawnPlayer;
    }

    public void Serialize(BinaryWriter writer)
    {
        writer.Write(PlayerId);
        NetworkSerializationUtils.Serialize(writer, PlayerPosition);
        writer.Write(PlayerYAngle);
    }

    public void Deserialize(BinaryReader reader)
    {
        PlayerId = reader.ReadUInt32();
        NetworkSerializationUtils.Deserialize(reader, ref PlayerPosition);
        PlayerYAngle = reader.ReadSingle();
    }
}