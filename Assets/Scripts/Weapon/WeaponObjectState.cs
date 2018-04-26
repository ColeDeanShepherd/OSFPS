using System.IO;

public class WeaponObjectState : INetworkSerializable
{
    public uint Id;
    public WeaponType Type = WeaponType.Pistol;
    public RigidBodyState RigidBodyState = new RigidBodyState();

    public void Deserialize(BinaryReader reader)
    {
        Id = reader.ReadUInt32();
        Type = (WeaponType)reader.ReadByte();
        RigidBodyState.Deserialize(reader);
    }

    public void Serialize(BinaryWriter writer)
    {
        writer.Write(Id);
        writer.Write((byte)Type);
        RigidBodyState.Serialize(writer);
    }
}