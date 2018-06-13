using System.IO;
using UnityEngine;

[System.Serializable]
public class EquippedWeaponState : INetworkSerializable
{
    public WeaponType Type = WeaponType.Pistol;
    public ushort BulletsLeftInMagazine;
    public ushort BulletsLeftOutOfMagazine;
    public float TimeUntilCanShoot
    {
        get
        {
            return Mathf.Max(Definition.ShotInterval - TimeSinceLastShot, 0);
        }
    }
    public float TimeSinceLastShot;

    public ushort BulletsLeft
    {
        get
        {
            return (ushort)(BulletsLeftInMagazine + BulletsLeftOutOfMagazine);
        }
    }
    public ushort BulletsShotFromMagazine
    {
        get
        {
            return (ushort)(Definition.BulletsPerMagazine - BulletsLeftInMagazine);
        }
    }
    public WeaponDefinition Definition
    {
        get
        {
            return WeaponSystem.Instance.GetWeaponDefinitionByType(Type);
        }
    }

    public void Deserialize(BinaryReader reader)
    {
        Type = (WeaponType)reader.ReadByte();
        BulletsLeftInMagazine = reader.ReadUInt16();
        BulletsLeftOutOfMagazine = reader.ReadUInt16();
        TimeSinceLastShot = reader.ReadSingle();
    }

    public void Serialize(BinaryWriter writer)
    {
        writer.Write((byte)Type);
        writer.Write(BulletsLeftInMagazine);
        writer.Write(BulletsLeftOutOfMagazine);
        writer.Write(TimeSinceLastShot);
    }
}