using System;
using System.Collections.Generic;
using System.IO;

public class GameState : INetworkSerializable
{
    public List<PlayerState> Players = new List<PlayerState>();
    public List<DynamicObjectState> DynamicObjects = new List<DynamicObjectState>();

    public void Serialize(BinaryWriter writer)
    {
        NetworkSerializationUtils.Serialize(writer, Players);
        NetworkSerializationUtils.Serialize(
            writer, DynamicObjects, serializeElementFunc: NetworkSerializationUtils.SerializeDynamicObjectState
        );
    }
    public void Deserialize(BinaryReader reader)
    {
        NetworkSerializationUtils.Deserialize(reader, Players);
        NetworkSerializationUtils.Deserialize(
            reader, DynamicObjects, deserializeElementFunc: NetworkSerializationUtils.DeserializeDynamicObjectState
        );
    }
}