using System.IO;

// Server <-> Client
public class DetonateGrenadeMessage : INetworkMessage
{
    public uint Id;

    public NetworkMessageType GetMessageType()
    {
        return NetworkMessageType.DetonateGrenade;
    }

    public void Serialize(BinaryWriter writer)
    {
        writer.Write(Id);
    }
    public void Deserialize(BinaryReader reader)
    {
        Id = reader.ReadUInt32();
    }
}