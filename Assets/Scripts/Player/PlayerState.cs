using System.IO;
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
    public WeaponState Weapon0;
    public WeaponState Weapon1;
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
            return Weapon0;
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
                !IsReloading;
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
    public bool IsReloading
    {
        get
        {
            return ReloadTimeLeft >= 0;
        }
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
        NetworkSerializationUtils.SerializeNullable(writer, Weapon0);
        NetworkSerializationUtils.SerializeNullable(writer, Weapon1);
        writer.Write(ReloadTimeLeft);
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
        Weapon0 = NetworkSerializationUtils.DeserializeNullable<WeaponState>(reader);
        Weapon1 = NetworkSerializationUtils.DeserializeNullable<WeaponState>(reader);
        ReloadTimeLeft = reader.ReadSingle();
    }
}