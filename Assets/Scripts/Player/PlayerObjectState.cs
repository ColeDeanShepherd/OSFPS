using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

[System.Serializable]
public class PlayerObjectState : INetworkSerializable
{
    public uint Id;
    public Vector3 Position;
    public Vector3 Velocity;
    public Vector2 LookDirAngles;

    public PlayerInput Input;

    public float Shield;
    public float Health;
    public float TimeUntilShieldCanRegen;

    public WeaponObjectState[] Weapons = new WeaponObjectState[OsFps.MaxWeaponCount];
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
    public WeaponObjectState CurrentWeapon
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
                (CurrentWeapon.BulletsLeftInMagazine < CurrentWeapon.Definition.BulletsPerMagazine);
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

    public void Serialize(BinaryWriter writer)
    {
        writer.Write(Id);
        NetworkSerializationUtils.Serialize(writer, Position);
        NetworkSerializationUtils.Serialize(writer, Velocity);
        NetworkSerializationUtils.Serialize(writer, LookDirAngles);
        NetworkSerializationUtils.SerializeObject(writer, Input);
        writer.Write(Shield);
        writer.Write(Health);
        writer.Write(TimeUntilShieldCanRegen);

        for (var i = 0; i < Weapons.Length; i++)
        {
            NetworkSerializationUtils.SerializeNullable(writer, Weapons[i]);
        }

        writer.Write(CurrentWeaponIndex);
        writer.Write(ReloadTimeLeft);
        writer.Write(TimeUntilCanThrowGrenade);

        writer.Write(CurrentGrenadeSlotIndex);

        for (var i = 0; i < GrenadeSlots.Length; i++)
        {
            var grenadeSlot = GrenadeSlots[i];
            NetworkSerializationUtils.SerializeObject(
                writer, grenadeSlot, overrideType: null, isNullableIfReferenceType: true
            );
        }
    }
    public void Deserialize(BinaryReader reader)
    {
        Id = reader.ReadUInt32();
        NetworkSerializationUtils.Deserialize(reader, ref Position);
        NetworkSerializationUtils.Deserialize(reader, ref Velocity);
        NetworkSerializationUtils.Deserialize(reader, ref LookDirAngles);
        Input = NetworkSerializationUtils.Deserialize<PlayerInput>(reader);
        Shield = reader.ReadSingle();
        Health = reader.ReadSingle();
        TimeUntilShieldCanRegen = reader.ReadSingle();

        for (var i = 0; i < Weapons.Length; i++)
        {
            Weapons[i] = NetworkSerializationUtils.DeserializeNullable<WeaponObjectState>(reader);
        }

        CurrentWeaponIndex = reader.ReadByte();
        ReloadTimeLeft = reader.ReadSingle();

        TimeUntilCanThrowGrenade = reader.ReadSingle();
        CurrentGrenadeSlotIndex = reader.ReadByte();

        for (var i = 0; i < GrenadeSlots.Length; i++)
        {
            GrenadeSlots[i] = NetworkSerializationUtils.Deserialize<GrenadeSlot>(
                reader, isNullableIfReferenceType: true
            );
        }
    }
}