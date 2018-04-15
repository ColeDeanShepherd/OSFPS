using System.IO;

// Server <-> Client
public class ReloadPressedMessage : INetworkMessage
{
    public uint PlayerId;

    public NetworkMessageType GetMessageType()
    {
        return NetworkMessageType.ReloadPressed;
    }

    public void Serialize(BinaryWriter writer)
    {
        writer.Write(PlayerId);
    }
    public void Deserialize(BinaryReader reader)
    {
        PlayerId = reader.ReadUInt32();
    }
}