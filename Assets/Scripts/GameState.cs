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
            NetworkSerializationUtils.SerializeGivenChangeMask(
                binaryWriter, player.GetType(), player, uint.MaxValue
            );
        });
        NetworkSerializationUtils.Serialize(writer, PlayerObjects, (binaryWriter, playerObject) =>
        {
            NetworkSerializationUtils.SerializeGivenChangeMask(
                binaryWriter, playerObject.GetType(), playerObject, uint.MaxValue
            );
        });
        NetworkSerializationUtils.Serialize(writer, WeaponObjects, (binaryWriter, weaponObject) =>
        {
            NetworkSerializationUtils.SerializeGivenChangeMask(
                binaryWriter, weaponObject.GetType(), weaponObject, uint.MaxValue
            );
        });
        NetworkSerializationUtils.Serialize(writer, WeaponSpawners, (binaryWriter, weaponSpawner) =>
        {
            NetworkSerializationUtils.SerializeGivenChangeMask(
                binaryWriter, weaponSpawner.GetType(), weaponSpawner, uint.MaxValue
            );
        });
        NetworkSerializationUtils.Serialize(writer, Grenades, (binaryWriter, grenade) =>
        {
            NetworkSerializationUtils.SerializeGivenChangeMask(
                binaryWriter, grenade.GetType(), grenade, uint.MaxValue
            );
        });
        NetworkSerializationUtils.Serialize(writer, GrenadeSpawners, (binaryWriter, grenadeSpawner) =>
        {
            NetworkSerializationUtils.SerializeGivenChangeMask(
                binaryWriter, grenadeSpawner.GetType(), grenadeSpawner, uint.MaxValue
            );
        });
        NetworkSerializationUtils.Serialize(writer, Rockets, (binaryWriter, rocket) =>
        {
            NetworkSerializationUtils.SerializeGivenChangeMask(
                binaryWriter, rocket.GetType(), rocket, uint.MaxValue
            );
        });
    }
    public void Deserialize(BinaryReader reader)
    {
        SequenceNumber = reader.ReadUInt32();
        NetworkSerializationUtils.Deserialize(reader, Players, binaryReader =>
        {
            var playerState = new PlayerState();
            NetworkSerializationUtils.DeserializeGivenChangeMask(
                binaryReader, playerState.GetType(), playerState, uint.MaxValue
            );
            return playerState;
        });

        NetworkSerializationUtils.Deserialize(reader, PlayerObjects, binaryReader =>
        {
            var playerObjectState = new PlayerObjectState();
            NetworkSerializationUtils.DeserializeGivenChangeMask(
                binaryReader, playerObjectState.GetType(), playerObjectState, uint.MaxValue
            );
            return playerObjectState;
        });

        NetworkSerializationUtils.Deserialize(reader, WeaponObjects, binaryReader =>
        {
            var weaponObjectState = new WeaponObjectState();
            NetworkSerializationUtils.DeserializeGivenChangeMask(
                binaryReader, weaponObjectState.GetType(), weaponObjectState, uint.MaxValue
            );
            return weaponObjectState;
        });

        NetworkSerializationUtils.Deserialize(reader, WeaponSpawners, binaryReader =>
        {
            var weaponSpawnerState = new WeaponSpawnerState();
            NetworkSerializationUtils.DeserializeGivenChangeMask(
                binaryReader, weaponSpawnerState.GetType(), weaponSpawnerState, uint.MaxValue
            );
            return weaponSpawnerState;
        });

        NetworkSerializationUtils.Deserialize(reader, Grenades, binaryReader =>
        {
            var grenadeState = new GrenadeState();
            NetworkSerializationUtils.DeserializeGivenChangeMask(
                binaryReader, grenadeState.GetType(), grenadeState, uint.MaxValue
            );
            return grenadeState;
        });

        NetworkSerializationUtils.Deserialize(reader, GrenadeSpawners, binaryReader =>
        {
            var grenadeSpawnerState = new GrenadeSpawnerState();
            NetworkSerializationUtils.DeserializeGivenChangeMask(
                binaryReader, grenadeSpawnerState.GetType(), grenadeSpawnerState, uint.MaxValue
            );
            return grenadeSpawnerState;
        });

        NetworkSerializationUtils.Deserialize(reader, Rockets, binaryReader =>
        {
            var rocketState = new RocketState();
            NetworkSerializationUtils.DeserializeGivenChangeMask(
                binaryReader, rocketState.GetType(), rocketState, uint.MaxValue
            );
            return rocketState;
        });
    }
}