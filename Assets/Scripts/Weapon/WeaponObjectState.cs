using System.IO;

[System.Serializable]
public class WeaponObjectState : INetworkSerializable
{
    public uint Id;
    public WeaponType Type = WeaponType.Pistol;
    public ushort BulletsLeftInMagazine;
    public ushort BulletsLeftOutOfMagazine;
    public float TimeUntilCanShoot;
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
            return OsFps.GetWeaponDefinitionByType(Type);
        }
    }

    public void Deserialize(BinaryReader reader)
    {
        Id = reader.ReadUInt32();
        Type = (WeaponType)reader.ReadByte();
        BulletsLeftInMagazine = reader.ReadUInt16();
        BulletsLeftOutOfMagazine = reader.ReadUInt16();
        TimeUntilCanShoot = reader.ReadSingle();
        RigidBodyState.Deserialize(reader);
    }

    public void Serialize(BinaryWriter writer)
    {
        writer.Write(Id);
        writer.Write((byte)Type);
        writer.Write(BulletsLeftInMagazine);
        writer.Write(BulletsLeftOutOfMagazine);
        writer.Write(TimeUntilCanShoot);
        RigidBodyState.Serialize(writer);
    }
}