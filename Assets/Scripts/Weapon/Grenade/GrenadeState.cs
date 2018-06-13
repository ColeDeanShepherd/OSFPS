using System.IO;

[System.Serializable]
public class GrenadeState : INetworkSerializable
{
    public uint Id;
    public GrenadeType Type;
    public RigidBodyState RigidBodyState = new RigidBodyState();
    public bool IsActive;
    public float? TimeUntilDetonation;

    // not network serialized
    public uint? ThrowerPlayerId;
    public uint? GrenadeSpawnerId;

    public void Deserialize(BinaryReader reader)
    {
        Id = reader.ReadUInt32();
        Type = (GrenadeType)reader.ReadByte();
        RigidBodyState.Deserialize(reader);
        IsActive = reader.ReadBoolean();
        TimeUntilDetonation = NetworkSerializationUtils.Deserialize<float?>(reader);
    }

    public void Serialize(BinaryWriter writer)
    {
        writer.Write(Id);
        writer.Write((byte)Type);
        RigidBodyState.Serialize(writer);
        writer.Write(IsActive);
        NetworkSerializationUtils.Serialize(writer, TimeUntilDetonation);
    }
}