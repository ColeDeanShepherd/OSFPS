using System;
using System.Collections.Generic;
using System.IO;

public class GameState : INetworkSerializable
{
    public uint SequenceNumber;
    public List<PlayerState> Players = new List<PlayerState>();
    public List<PlayerObjectState> PlayerObjects = new List<PlayerObjectState>();
    public List<WeaponObjectState> WeaponObjects = new List<WeaponObjectState>();
    public List<WeaponSpawnerState> WeaponSpawners = new List<WeaponSpawnerState>();
    public List<GrenadeState> Grenades = new List<GrenadeState>();
    public List<GrenadeSpawnerState> GrenadeSpawners = new List<GrenadeSpawnerState>();
    public List<RocketState> Rockets = new List<RocketState>();

    public void Serialize(BinaryWriter writer)
    {
        writer.Write(SequenceNumber);
        NetworkSerializationUtils.Serialize(writer, Players, (binaryWriter, player) =>
        {
            var changeMask = uint.MaxValue;

            binaryWriter.Write(changeMask);
            NetworkSerializationUtils.SerializeGivenChangeMask(
                binaryWriter, player.GetType(), player, changeMask
            );
        });
        NetworkSerializationUtils.Serialize(writer, PlayerObjects, (binaryWriter, playerObject) =>
        {
            var changeMask = uint.MaxValue;

            binaryWriter.Write(changeMask);
            NetworkSerializationUtils.SerializeGivenChangeMask(
                binaryWriter, playerObject.GetType(), playerObject, changeMask
            );
        });
        NetworkSerializationUtils.Serialize(writer, WeaponObjects, (binaryWriter, weaponObject) =>
        {
            var changeMask = uint.MaxValue;

            binaryWriter.Write(changeMask);
            NetworkSerializationUtils.SerializeGivenChangeMask(
                binaryWriter, weaponObject.GetType(), weaponObject, changeMask
            );
        });
        NetworkSerializationUtils.Serialize(writer, WeaponSpawners, (binaryWriter, weaponSpawner) =>
        {
            var changeMask = uint.MaxValue;

            binaryWriter.Write(changeMask);
            NetworkSerializationUtils.SerializeGivenChangeMask(
                binaryWriter, weaponSpawner.GetType(), weaponSpawner, changeMask
            );
        });
        NetworkSerializationUtils.Serialize(writer, Grenades, (binaryWriter, grenade) =>
        {
            var changeMask = uint.MaxValue;

            binaryWriter.Write(changeMask);
            NetworkSerializationUtils.SerializeGivenChangeMask(
                binaryWriter, grenade.GetType(), grenade, changeMask
            );
        });
        NetworkSerializationUtils.Serialize(writer, GrenadeSpawners, (binaryWriter, grenadeSpawner) =>
        {
            var changeMask = uint.MaxValue;

            binaryWriter.Write(changeMask);
            NetworkSerializationUtils.SerializeGivenChangeMask(
                binaryWriter, grenadeSpawner.GetType(), grenadeSpawner, changeMask
            );
        });
        NetworkSerializationUtils.Serialize(writer, Rockets, (binaryWriter, rocket) =>
        {
            var changeMask = uint.MaxValue;

            binaryWriter.Write(changeMask);
            NetworkSerializationUtils.SerializeGivenChangeMask(
                binaryWriter, rocket.GetType(), rocket, changeMask
            );
        });
    }
    public void Deserialize(BinaryReader reader)
    {
        SequenceNumber = reader.ReadUInt32();
        NetworkSerializationUtils.Deserialize(reader, Players, binaryReader =>
        {
            var playerState = new PlayerState();
            NetworkSerializationUtils.DeserializeDelta(
                binaryReader, playerState.GetType(), playerState
            );
            return playerState;
        });

        NetworkSerializationUtils.Deserialize(reader, PlayerObjects, binaryReader =>
        {
            var playerObjectState = new PlayerObjectState();
            NetworkSerializationUtils.DeserializeDelta(
                binaryReader, playerObjectState.GetType(), playerObjectState
            );
            return playerObjectState;
        });

        NetworkSerializationUtils.Deserialize(reader, WeaponObjects, binaryReader =>
        {
            var weaponObjectState = new WeaponObjectState();
            NetworkSerializationUtils.DeserializeDelta(
                binaryReader, weaponObjectState.GetType(), weaponObjectState
            );
            return weaponObjectState;
        });

        NetworkSerializationUtils.Deserialize(reader, WeaponSpawners, binaryReader =>
        {
            var weaponSpawnerState = new WeaponSpawnerState();
            NetworkSerializationUtils.DeserializeDelta(
                binaryReader, weaponSpawnerState.GetType(), weaponSpawnerState
            );
            return weaponSpawnerState;
        });

        NetworkSerializationUtils.Deserialize(reader, Grenades, binaryReader =>
        {
            var grenadeState = new GrenadeState();
            NetworkSerializationUtils.DeserializeDelta(
                binaryReader, grenadeState.GetType(), grenadeState
            );
            return grenadeState;
        });

        NetworkSerializationUtils.Deserialize(reader, GrenadeSpawners, binaryReader =>
        {
            var grenadeSpawnerState = new GrenadeSpawnerState();
            NetworkSerializationUtils.DeserializeDelta(
                binaryReader, grenadeSpawnerState.GetType(), grenadeSpawnerState
            );
            return grenadeSpawnerState;
        });

        NetworkSerializationUtils.Deserialize(reader, Rockets, binaryReader =>
        {
            var rocketState = new RocketState();
            NetworkSerializationUtils.DeserializeDelta(
                binaryReader, rocketState.GetType(), rocketState
            );
            return rocketState;
        });
    }
}