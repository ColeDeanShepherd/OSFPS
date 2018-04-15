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

    public bool IsAlive
    {
        get
        {
            return Health > 0;
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
    }
}