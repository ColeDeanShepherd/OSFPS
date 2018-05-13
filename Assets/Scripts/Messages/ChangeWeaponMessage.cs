using System.IO;

// Server <-> Client
public class ChangeWeaponMessage : INetworkMessage
{
    public uint PlayerId;
    public byte WeaponIndex;

    public NetworkMessageType GetMessageType()
    {
        return NetworkMessageType.ChangeWeapon;
    }

    public void Serialize(BinaryWriter writer)
    {
        writer.Write(PlayerId);
        writer.Write(WeaponIndex);
    }
    public void Deserialize(BinaryReader reader)
    {
        PlayerId = reader.ReadUInt32();
        WeaponIndex = reader.ReadByte();
    }
}