using System.IO;
using UnityEngine;

// Server -> Client
public class SpawnPlayerMessage : INetworkMessage
{
    public uint PlayerId;
    public Vector3 PlayerPosition;
    public float PlayerLookDirYAngle;

    public NetworkMessageType GetMessageType()
    {
        return NetworkMessageType.SpawnPlayer;
    }

    public void Serialize(BinaryWriter writer)
    {
        writer.Write(PlayerId);
        NetworkSerializationUtils.Serialize(writer, PlayerPosition);
        writer.Write(PlayerLookDirYAngle);
    }

    public void Deserialize(BinaryReader reader)
    {
        PlayerId = reader.ReadUInt32();
        NetworkSerializationUtils.Deserialize(reader, ref PlayerPosition);
        PlayerLookDirYAngle = reader.ReadSingle();
    }
}