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
        byte changeBits = byte.MaxValue;

        if (BitUtilities.GetBit(changeBits, 0))
        {
            writer.Write(Id);
        }

        if (BitUtilities.GetBit(changeBits, 1))
        {
            writer.Write(Name);
        }

        if (BitUtilities.GetBit(changeBits, 2))
        {
            writer.Write(Kills);
        }

        if (BitUtilities.GetBit(changeBits, 3))
        {
            writer.Write(Deaths);
        }

        if (BitUtilities.GetBit(changeBits, 4))
        {
            writer.Write(RespawnTimeLeft);
        }
    }
    public void Deserialize(BinaryReader reader)
    {
        byte changeBits = byte.MaxValue;

        if (BitUtilities.GetBit(changeBits, 0))
        {
            Id = reader.ReadUInt32();
        }

        if (BitUtilities.GetBit(changeBits, 1))
        {
            Name = reader.ReadString();
        }

        if (BitUtilities.GetBit(changeBits, 2))
        {
            Kills = reader.ReadInt16();
        }

        if (BitUtilities.GetBit(changeBits, 3))
        {
            Deaths = reader.ReadUInt16();
        }

        if (BitUtilities.GetBit(changeBits, 4))
        {
            RespawnTimeLeft = reader.ReadSingle();
        }
    }
}