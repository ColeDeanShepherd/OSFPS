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

    public void Serialize(BinaryWriter writer)
    {
        NetworkSerializationUtils.Serialize(writer, Players);
        NetworkSerializationUtils.Serialize(writer, PlayerObjects);
        NetworkSerializationUtils.Serialize(writer, WeaponObjects);
        NetworkSerializationUtils.Serialize(writer, WeaponSpawners);
        NetworkSerializationUtils.Serialize(writer, Grenades);
    }
    public void Deserialize(BinaryReader reader)
    {
        NetworkSerializationUtils.Deserialize(reader, Players);
        NetworkSerializationUtils.Deserialize(reader, PlayerObjects);
        NetworkSerializationUtils.Deserialize(reader, WeaponObjects);
        NetworkSerializationUtils.Deserialize(reader, WeaponSpawners);
        NetworkSerializationUtils.Deserialize(reader, Grenades);
    }
}