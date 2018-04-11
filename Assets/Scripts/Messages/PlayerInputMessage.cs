using System.IO;

// Server <-> Client
public class PlayerInputMessage : INetworkMessage
{
    public uint PlayerId;
    public PlayerInput PlayerInput;

    public NetworkMessageType GetMessageType()
    {
        return NetworkMessageType.PlayerInput;
    }

    public void Serialize(BinaryWriter writer)
    {
        writer.Write(PlayerId);
        PlayerInput.Serialize(writer);
    }
    public void Deserialize(BinaryReader reader)
    {
        PlayerId = reader.ReadUInt32();
        PlayerInput.Deserialize(reader);
    }
}