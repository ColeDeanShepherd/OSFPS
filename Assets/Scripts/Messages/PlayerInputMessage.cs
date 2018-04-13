using System.IO;
using UnityEngine;

// Server <-> Client
public class PlayerInputMessage : INetworkMessage
{
    public uint PlayerId;
    public PlayerInput PlayerInput;
    public Vector2 LookDirAngles;

    public NetworkMessageType GetMessageType()
    {
        return NetworkMessageType.PlayerInput;
    }

    public void Serialize(BinaryWriter writer)
    {
        writer.Write(PlayerId);
        PlayerInput.Serialize(writer);
        NetworkSerializationUtils.Serialize(writer, LookDirAngles);
    }
    public void Deserialize(BinaryReader reader)
    {
        PlayerId = reader.ReadUInt32();
        PlayerInput.Deserialize(reader);
        NetworkSerializationUtils.Deserialize(reader, ref LookDirAngles);
    }
}