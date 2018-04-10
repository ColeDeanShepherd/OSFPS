using System.IO;

// Server -> Client
public class SpawnPlayerMessage : NetworkMessage
{
    public uint PlayerId;

    public override NetworkMessageType GetMessageType()
    {
        return NetworkMessageType.SpawnPlayer;
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