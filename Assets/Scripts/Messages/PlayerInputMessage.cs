using System.IO;
using UnityEngine;

// Server <-> Client
public class PlayerInputMessage : INetworkMessage
{
    public uint PlayerId;
    public PlayerInput PlayerInput;
    public Vector3 EulerAngles;

    public NetworkMessageType GetMessageType()
    {
        return NetworkMessageType.PlayerInput;
    }

    public void Serialize(BinaryWriter writer)
    {
        writer.Write(PlayerId);
        PlayerInput.Serialize(writer);
        writer.Write(EulerAngles.x);
        writer.Write(EulerAngles.y);
    }
    public void Deserialize(BinaryReader reader)
    {
        PlayerId = reader.ReadUInt32();
        PlayerInput.Deserialize(reader);
        EulerAngles = new Vector3(reader.ReadSingle(), reader.ReadSingle(), 0);
    }
}