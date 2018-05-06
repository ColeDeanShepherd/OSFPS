using System.IO;

// Server <-> Client
public class ChatMessage : INetworkMessage
{
    public uint PlayerId;
    public string Message;

    public NetworkMessageType GetMessageType()
    {
        return NetworkMessageType.Chat;
    }

    public void Serialize(BinaryWriter writer)
    {
        writer.Write(PlayerId);
        writer.Write(Message);
    }
    public void Deserialize(BinaryReader reader)
    {
        PlayerId = reader.ReadUInt32();
        Message = reader.ReadString();
    }
}