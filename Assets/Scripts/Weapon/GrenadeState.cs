using System.IO;

public class GrenadeState : INetworkSerializable
{
    public uint Id;
    public GrenadeType Type;
    public RigidBodyState RigidBodyState = new RigidBodyState();
    public bool IsFuseBurning;
    public float TimeUntilDetonation;

    public void Deserialize(BinaryReader reader)
    {
        Id = reader.ReadUInt32();
        Type = (GrenadeType)reader.ReadByte();
        RigidBodyState.Deserialize(reader);
        IsFuseBurning = reader.ReadBoolean();
        TimeUntilDetonation = reader.ReadSingle();
    }

    public void Serialize(BinaryWriter writer)
    {
        writer.Write(Id);
        writer.Write((byte)Type);
        RigidBodyState.Serialize(writer);
        writer.Write(IsFuseBurning);
        writer.Write(TimeUntilDetonation);
    }
}