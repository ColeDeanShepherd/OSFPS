using System.IO;

[System.Serializable]
public class GrenadeSpawnerState : INetworkSerializable
{
    public uint Id;
    public GrenadeType Type;
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
        Type = (GrenadeType)reader.ReadByte();
        TimeUntilNextSpawn = NetworkSerializationUtils.Deserialize<float?>(reader);
    }
}