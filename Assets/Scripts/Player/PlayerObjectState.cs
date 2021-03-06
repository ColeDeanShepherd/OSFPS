﻿using System;
using System.Linq;
using NetworkLibrary;
using Unity.Mathematics;

[System.Serializable]
[NetworkedComponent(MonoBehaviourType = typeof(PlayerObjectComponent))]
public class PlayerObjectState
{
    public uint Id;
    public float3 Position;
    public float3 Velocity;
    public float2 LookDirAngles;

    public PlayerInput Input;

    public float Shield;
    public float Health;
    public float TimeUntilShieldCanRegen;

    public EquippedWeaponState[] Weapons = new EquippedWeaponState[OsFps.MaxWeaponCount];
    public byte CurrentWeaponIndex;
    public float ReloadTimeLeft;
    public float EquipWeaponTimeLeft;
    public float RecoilTimeLeft;

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
    public bool CanTryToFireWeapon
    {
        get
        {
            return
                IsAlive &&
                (CurrentWeapon != null) &&
                !IsEquippingWeapon &&
                !IsReloading &&
                (CurrentWeapon.TimeUntilCanShoot <= 0);
        }
    }
    public bool CanShoot
    {
        get
        {
            return CanTryToFireWeapon && (CurrentWeapon.BulletsLeftInMagazine > 0);
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
    public bool IsWeaponRecoiling
    {
        get
        {
            return RecoilTimeLeft >= 0;
        }
    }
    public bool IsEquippingWeapon
    {
        get
        {
            return EquipWeaponTimeLeft >= 0;
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