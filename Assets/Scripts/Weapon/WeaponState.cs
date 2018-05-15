using System;
using System.IO;
using System.Linq;
using UnityEngine;

public class WeaponState : INetworkSerializable
{
    public WeaponType Type;
    public ushort BulletsLeft;
    public ushort BulletsLeftInMagazine;
    public float TimeUntilCanShoot;

    public WeaponState()
    {
    }
    public WeaponState(WeaponType type, ushort bulletsLeft)
    {
        Type = type;
        BulletsLeft = (ushort)Mathf.Min(bulletsLeft, Definition.MaxAmmo);
        BulletsLeftInMagazine = (ushort)Mathf.Min(BulletsLeft, Definition.BulletsPerMagazine);
    }

    public ushort BulletsLeftOutOfMagazine
    {
        get
        {
            return (ushort)(BulletsLeft - BulletsLeftInMagazine);
        }
    }
    public WeaponDefinition Definition
    {
        get
        {
            return OsFps.GetWeaponDefinitionByType(Type);
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