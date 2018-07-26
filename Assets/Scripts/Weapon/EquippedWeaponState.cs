using System.IO;
using UnityEngine;

[System.Serializable]
public class EquippedWeaponState
{
    public WeaponType Type = WeaponType.Pistol;
    public ushort BulletsLeftInMagazine;
    public ushort BulletsLeftOutOfMagazine;
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
    public ushort BulletsUsed
    {
        get
        {
            return (ushort)(Definition.MaxAmmo - BulletsLeft);
        }
    }
    public WeaponDefinition Definition
    {
        get
        {
            return WeaponSystem.Instance.GetWeaponDefinitionByType(Type);
        }
    }
    public float TimeUntilCanShoot
    {
        get
        {
            return Mathf.Max(Definition.ShotInterval - TimeSinceLastShot, 0);
        }
    }
}