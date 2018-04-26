using System;
using System.Collections.Generic;
using System.IO;

public class GameState : INetworkSerializable
{
    public List<PlayerState> Players = new List<PlayerState>();
    public List<WeaponObjectState> WeaponObjects = new List<WeaponObjectState>();

    public void Serialize(BinaryWriter writer)
    {
        NetworkSerializationUtils.Serialize(writer, Players);
        NetworkSerializationUtils.Serialize(writer, WeaponObjects);
    }
    public void Deserialize(BinaryReader reader)
    {
        NetworkSerializationUtils.Deserialize(reader, Players);
        NetworkSerializationUtils.Deserialize(reader, WeaponObjects);
    }
}