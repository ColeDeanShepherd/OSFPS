using System.IO;
using UnityEngine;

public class PlayerState : INetworkSerializable
{
    public uint Id;
    public Vector3 Position;
    public Vector3 EulerAngles;
    public PlayerInput Input;

    public void Serialize(BinaryWriter writer)
    {
        writer.Write(Id);
        NetworkSerializationUtils.Serialize(writer, Position);
        NetworkSerializationUtils.Serialize(writer, EulerAngles);
        Input.Serialize(writer);
    }
    public void Deserialize(BinaryReader reader)
    {
        Id = reader.ReadUInt32();
        NetworkSerializationUtils.Deserialize(reader, ref Position);
        NetworkSerializationUtils.Deserialize(reader, ref EulerAngles);
        Input.Deserialize(reader);
    }
}