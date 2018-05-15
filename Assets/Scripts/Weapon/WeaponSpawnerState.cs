using System.IO;

public class WeaponSpawnerState : INetworkSerializable
{
    public uint Id;
    public WeaponType Type;
    public float TimeUntilNextSpawn;

    public void Serialize(BinaryWriter writer)
    {
        writer.Write(Id);
        writer.Write((byte)Type);
        writer.Write(TimeUntilNextSpawn);
    }
    public void Deserialize(BinaryReader reader)
    {
        Id = reader.ReadUInt32();
        Type = (WeaponType)reader.ReadByte();
        TimeUntilNextSpawn = reader.ReadSingle();
    }
}