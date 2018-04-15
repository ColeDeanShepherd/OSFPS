using System;
using System.IO;

public class WeaponState : INetworkSerializable
{
    public WeaponType Type;
    public ushort BulletsLeft;
    public ushort BulletsLeftInMagazine;

    public ushort BulletsLeftOutOfMagazine
    {
        get
        {
            return (ushort)(BulletsLeft - BulletsLeftInMagazine);
        }
    }
    public ushort BulletsPerMagazine
    {
        get
        {
            switch (Type)
            {
                case WeaponType.Pistol:
                    return OsFps.PistolBulletsPerMagazine;
                default:
                    throw new NotImplementedException();
            }
        }
    }
    public ushort MaxAmmo
    {
        get
        {
            switch (Type)
            {
                case WeaponType.Pistol:
                    return OsFps.PistolMaxAmmo;
                default:
                    throw new NotImplementedException();
            }
        }
    }

    public void Serialize(BinaryWriter writer)
    {
        writer.Write((byte)Type);
        writer.Write(BulletsLeft);
        writer.Write(BulletsLeftInMagazine);
    }
    public void Deserialize(BinaryReader reader)
    {
        Type = (WeaponType)reader.ReadByte();
        BulletsLeft = reader.ReadUInt16();
        BulletsLeftInMagazine = reader.ReadUInt16();
    }
}