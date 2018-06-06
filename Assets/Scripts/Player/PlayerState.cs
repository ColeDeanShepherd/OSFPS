using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

[System.Serializable]
public class PlayerState : INetworkSerializable
{
    public uint Id;
    public string Name;
    public short Kills;
    public ushort Deaths;
    public float RespawnTimeLeft;

    public void Serialize(BinaryWriter writer)
    {
        writer.Write(Id);
        writer.Write(Name);
        writer.Write(Kills);
        writer.Write(Deaths);
        writer.Write(RespawnTimeLeft);
    }
    public void Deserialize(BinaryReader reader)
    {
        Id = reader.ReadUInt32();
        Name = reader.ReadString();
        Kills = reader.ReadInt16();
        Deaths = reader.ReadUInt16();
        RespawnTimeLeft = reader.ReadSingle();
    }
}