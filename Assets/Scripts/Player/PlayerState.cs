using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public class PlayerState : INetworkSerializable
{
    public uint Id;
    public Vector3 Position;
    public Vector3 Velocity;
    public Vector2 LookDirAngles;
    public PlayerInput Input;
    public int Health;
    public float RespawnTimeLeft;
    public int Kills;
    public int Deaths;
    public WeaponState[] Weapons = new WeaponState[OsFps.MaxWeaponCount];
    public byte CurrentWeaponIndex;
    public float TimeUntilCanThrowGrenade;
    public GrenadeType CurrentGrenadeType;
    public Dictionary<GrenadeType, byte> GrenadesLeftByType;
    public float ReloadTimeLeft;

    public bool IsAlive
    {
        get
        {
            return Health > 0;
        }
    }
    public WeaponState CurrentWeapon
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
                (GrenadesLeftByType[CurrentGrenadeType] > 0);
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

    public PlayerState()
    {
        GrenadesLeftByType = Enum.GetValues(typeof(GrenadeType)).Cast<GrenadeType>()
            .ToDictionary(grenadeType => grenadeType, grenadeType => (byte)0);
    }
    public void Serialize(BinaryWriter writer)
    {
        writer.Write(Id);
        NetworkSerializationUtils.Serialize(writer, Position);
        NetworkSerializationUtils.Serialize(writer, Velocity);
        NetworkSerializationUtils.Serialize(writer, LookDirAngles);
        Input.Serialize(writer);
        writer.Write(Health);
        writer.Write(RespawnTimeLeft);
        writer.Write(Kills);
        writer.Write(Deaths);

        for (var i = 0; i < OsFps.MaxWeaponCount; i++)
        {
            NetworkSerializationUtils.SerializeNullable(writer, Weapons[i]);
        }

        writer.Write(CurrentWeaponIndex);
        writer.Write(ReloadTimeLeft);
        writer.Write(TimeUntilCanThrowGrenade);
        writer.Write((byte)CurrentGrenadeType);

        foreach (var grenadeType in Enum.GetValues(typeof(GrenadeType)).Cast<GrenadeType>())
        {
            writer.Write(GrenadesLeftByType[grenadeType]);
        }
    }
    public void Deserialize(BinaryReader reader)
    {
        Id = reader.ReadUInt32();
        NetworkSerializationUtils.Deserialize(reader, ref Position);
        NetworkSerializationUtils.Deserialize(reader, ref Velocity);
        NetworkSerializationUtils.Deserialize(reader, ref LookDirAngles);
        Input.Deserialize(reader);
        Health = reader.ReadInt32();
        RespawnTimeLeft = reader.ReadSingle();
        Kills = reader.ReadInt32();
        Deaths = reader.ReadInt32();

        for (var i = 0; i < OsFps.MaxWeaponCount; i++)
        {
            Weapons[i] = NetworkSerializationUtils.DeserializeNullable<WeaponState>(reader);
        }

        CurrentWeaponIndex = reader.ReadByte();
        ReloadTimeLeft = reader.ReadSingle();
        TimeUntilCanThrowGrenade = reader.ReadSingle();
        CurrentGrenadeType = (GrenadeType)reader.ReadByte();

        foreach (var grenadeType in Enum.GetValues(typeof(GrenadeType)).Cast<GrenadeType>())
        {
            GrenadesLeftByType[grenadeType] = reader.ReadByte();
        }
    }
}