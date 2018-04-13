using System.IO;

// Server <-> Client
public class TriggerPulledMessage : INetworkMessage
{
    public uint PlayerId;

    public NetworkMessageType GetMessageType()
    {
        return NetworkMessageType.TriggerPulled;
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