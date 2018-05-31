using System;
using System.Collections.Generic;
using System.IO;

public class GameState : INetworkSerializable
{
    public List<PlayerState> Players = new List<PlayerState>();
    public List<PlayerObjectState> PlayerObjects = new List<PlayerObjectState>();
    public List<WeaponObjectState> WeaponObjects = new List<WeaponObjectState>();
    public List<WeaponSpawnerState> WeaponSpawners = new List<WeaponSpawnerState>();
    public List<GrenadeState> Grenades = new List<GrenadeState>();
    public List<GrenadeSpawnerState> GrenadeSpawners = new List<GrenadeSpawnerState>();
    public List<RocketState> Rockets = new List<RocketState>();

    public void Serialize(BinaryWriter writer)
    {
        NetworkSerializationUtils.Serialize(writer, Players);
        NetworkSerializationUtils.Serialize(writer, PlayerObjects);
        NetworkSerializationUtils.Serialize(writer, WeaponObjects);
        NetworkSerializationUtils.Serialize(writer, WeaponSpawners);
        NetworkSerializationUtils.Serialize(writer, Grenades);
        NetworkSerializationUtils.Serialize(writer, GrenadeSpawners);
        NetworkSerializationUtils.Serialize(writer, Rockets);
    }
    public void Deserialize(BinaryReader reader)
    {
        NetworkSerializationUtils.Deserialize(reader, Players);
        NetworkSerializationUtils.Deserialize(reader, PlayerObjects);
        NetworkSerializationUtils.Deserialize(reader, WeaponObjects);
        NetworkSerializationUtils.Deserialize(reader, WeaponSpawners);
        NetworkSerializationUtils.Deserialize(reader, Grenades);
        NetworkSerializationUtils.Deserialize(reader, GrenadeSpawners);
        NetworkSerializationUtils.Deserialize(reader, Rockets);
    }
}