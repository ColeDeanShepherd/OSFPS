using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public class PlayerState : INetworkSerializable
{
    public uint Id;
    public short Kills;
    public ushort Deaths;
    public float RespawnTimeLeft;

    public void Serialize(BinaryWriter writer)
    {
        writer.Write(Id);
        writer.Write(Kills);
        writer.Write(Deaths);
        writer.Write(RespawnTimeLeft);
    }
    public void Deserialize(BinaryReader reader)
    {
        Id = reader.ReadUInt32();
        Kills = reader.ReadInt16();
        Deaths = reader.ReadUInt16();
        RespawnTimeLeft = reader.ReadSingle();
    }
}