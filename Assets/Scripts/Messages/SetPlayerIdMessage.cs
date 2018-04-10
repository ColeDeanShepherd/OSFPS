using System.IO;

// Server -> Client
public class SetPlayerIdMessage : NetworkMessage
{
    public uint PlayerId;

    public override NetworkMessageType GetMessageType()
    {
        return NetworkMessageType.SetPlayerIdMessage;
    }

    protected override void SerializeWithoutType(BinaryWriter writer)
    {
        writer.Write(PlayerId);
    }
    protected override void DeserializeWithoutType(BinaryReader reader)
    {
        PlayerId = reader.ReadUInt32();
    }
}