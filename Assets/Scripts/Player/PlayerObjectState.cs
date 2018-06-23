using System;
using System.Linq;
using UnityEngine;
using NetworkLibrary;

[System.Serializable]
[NetworkSynchronizedComponent(MonoBehaviourType = typeof(PlayerObjectComponent))]
public class PlayerObjectState
{
    public uint Id;
    public Vector3 Position;
    public Vector3 Velocity;
    public Vector2 LookDirAngles;

    public PlayerInput Input;

    public float Shield;
    public float Health;
    public float TimeUntilShieldCanRegen;

    public EquippedWeaponState[] Weapons = new EquippedWeaponState[OsFps.MaxWeaponCount];
    public byte CurrentWeaponIndex;
    public float ReloadTimeLeft;

    public float TimeUntilCanThrowGrenade;
    public byte CurrentGrenadeSlotIndex;
    public GrenadeSlot[] GrenadeSlots = new GrenadeSlot[OsFps.MaxGrenadeSlotCount];

    public bool IsAlive
    {
        get
        {
            // Rounding to compensate for accumulated floating point error.
            return Math.Round(Health, 2) > 0;
        }
    }
    public EquippedWeaponState CurrentWeapon
    {
        get
        {
            return Weapons[CurrentWeaponIndex];
        }
    }
    public bool CanShoot
    {
        get
        {
            return
                IsAlive &&
                (CurrentWeapon != null) &&
                (CurrentWeapon.BulletsLeftInMagazine > 0) &&
                !IsReloading &&
                (CurrentWeapon.TimeUntilCanShoot <= 0);
        }
    }
    public bool CanReload
    {
        get
        {
            return
                IsAlive &&
                (CurrentWeapon != null) &&
                !IsReloading &&
                (CurrentWeapon.BulletsLeftInMagazine < CurrentWeapon.Definition.BulletsPerMagazine) &&
                (CurrentWeapon.BulletsLeftOutOfMagazine > 0);
        }
    }
    public bool CanThrowGrenade
    {
        get
        {
            return
                IsAlive &&
                (TimeUntilCanThrowGrenade <= 0) &&
                ((GrenadeSlots[CurrentGrenadeSlotIndex]?.GrenadeCount ?? 0) > 0);
        }
    }
    public GrenadeSlot CurrentGrenadeSlot
    {
        get
        {
            return GrenadeSlots[CurrentGrenadeSlotIndex];
        }
    }
    public bool IsReloading
    {
        get
        {
            return ReloadTimeLeft >= 0;
        }
    }
    public bool HasEmptyWeapon
    {
        get
        {
            return Weapons.Any(w => w == null);
        }
    }
}