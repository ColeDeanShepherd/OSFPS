using System.IO;

// Server <-> Client
public class ThrowGrenadeMessage : INetworkMessage
{
    public uint PlayerId;

    public NetworkMessageType GetMessageType()
    {
        return NetworkMessageType.ThrowGrenade;
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