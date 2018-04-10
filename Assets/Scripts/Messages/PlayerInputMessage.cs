using System.IO;

// Server <-> Client
public class PlayerInputMessage : NetworkMessage
{
    public uint PlayerId;
    public PlayerInput PlayerInput;

    public override NetworkMessageType GetMessageType()
    {
        return NetworkMessageType.PlayerInput;
    }

    protected override void SerializeWithoutType(BinaryWriter writer)
    {
        writer.Write(PlayerId);
        PlayerInput.Serialize(writer);
    }
    protected override void DeserializeWithoutType(BinaryReader reader)
    {
        PlayerId = reader.ReadUInt32();
        PlayerInput.Deserialize(reader);
    }
}