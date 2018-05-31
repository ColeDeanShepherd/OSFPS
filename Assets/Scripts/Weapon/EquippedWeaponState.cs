using System.IO;

[System.Serializable]
public class EquippedWeaponState : INetworkSerializable
{
    public WeaponType Type = WeaponType.Pistol;
    public ushort BulletsLeftInMagazine;
    public ushort BulletsLeftOutOfMagazine;
    public float TimeUntilCanShoot;

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
        Type = (WeaponType)reader.ReadByte();
        BulletsLeftInMagazine = reader.ReadUInt16();
        BulletsLeftOutOfMagazine = reader.ReadUInt16();
        TimeUntilCanShoot = reader.ReadSingle();
    }

    public void Serialize(BinaryWriter writer)
    {
        writer.Write((byte)Type);
        writer.Write(BulletsLeftInMagazine);
        writer.Write(BulletsLeftOutOfMagazine);
        writer.Write(TimeUntilCanShoot);
    }
}