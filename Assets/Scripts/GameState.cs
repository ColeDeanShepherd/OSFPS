using System;
using System.Collections.Generic;
using System.IO;

public class GameState : INetworkSerializable
{
    public List<PlayerState> Players = new List<PlayerState>();

    public void Serialize(BinaryWriter writer)
    {
        NetworkSerializationUtils.Serialize(writer, Players);
    }
    public void Deserialize(BinaryReader reader)
    {
        NetworkSerializationUtils.Deserialize(reader, Players);
    }
}