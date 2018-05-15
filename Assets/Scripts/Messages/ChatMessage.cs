using System.IO;

// Server <-> Client
public class ChatMessage : INetworkMessage
{
    public uint? PlayerId;
    public string Message;

    public NetworkMessageType GetMessageType()
    {
        return NetworkMessageType.Chat;
    }

    public void Serialize(BinaryWriter writer)
    {
        writer.Write(PlayerId.HasValue);
        if (PlayerId.HasValue)
        {
            writer.Write(PlayerId.Value);
        }

        writer.Write(Message);
    }
    public void Deserialize(BinaryReader reader)
    {
        var playerIdHasValue = reader.ReadBoolean();
        if (playerIdHasValue)
        {
            PlayerId = reader.ReadUInt32();
        }

        Message = reader.ReadString();
    }
}