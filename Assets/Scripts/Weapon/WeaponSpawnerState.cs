using System.IO;

public class WeaponSpawnerState : INetworkSerializable
{
    public uint Id;
    public WeaponType Type;
    public float? TimeUntilNextSpawn;

    public void Serialize(BinaryWriter writer)
    {
        writer.Write(Id);
        writer.Write((byte)Type);
        NetworkSerializationUtils.Serialize(writer, TimeUntilNextSpawn);
    }
    public void Deserialize(BinaryReader reader)
    {
        Id = reader.ReadUInt32();
        Type = (WeaponType)reader.ReadByte();
        TimeUntilNextSpawn = NetworkSerializationUtils.Deserialize<float?>(reader);
    }
}