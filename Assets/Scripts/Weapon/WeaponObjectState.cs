using System.IO;

[System.Serializable]
public class WeaponObjectState : INetworkSerializable
{
    public uint Id;
    public WeaponType Type = WeaponType.Pistol;
    public ushort BulletsLeftInMagazine;
    public ushort BulletsLeftOutOfMagazine;
    public RigidBodyState RigidBodyState = new RigidBodyState();
    public uint? WeaponSpawnerId;

    public ushort BulletsLeft
    {
        get
        {
            return (ushort)(BulletsLeftInMagazine + BulletsLeftOutOfMagazine);
        }
    }
    public WeaponDefinition Definition
    {
        get
        {
            return OsFps.Instance.GetWeaponDefinitionByType(Type);
        }
    }

    public void Deserialize(BinaryReader reader)
    {
        Id = reader.ReadUInt32();
        Type = (WeaponType)reader.ReadByte();
        BulletsLeftInMagazine = reader.ReadUInt16();
        BulletsLeftOutOfMagazine = reader.ReadUInt16();
        RigidBodyState.Deserialize(reader);
    }

    public void Serialize(BinaryWriter writer)
    {
        writer.Write(Id);
        writer.Write((byte)Type);
        writer.Write(BulletsLeftInMagazine);
        writer.Write(BulletsLeftOutOfMagazine);
        RigidBodyState.Serialize(writer);
    }
}