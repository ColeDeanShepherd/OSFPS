using System.IO;

[System.Serializable]
public class RocketState : INetworkSerializable
{
    public uint Id;
    public RigidBodyState RigidBodyState = new RigidBodyState();
    public uint? ShooterPlayerId;

    public void Deserialize(BinaryReader reader)
    {
        Id = reader.ReadUInt32();
        RigidBodyState.Deserialize(reader);
    }

    public void Serialize(BinaryWriter writer)
    {
        writer.Write(Id);
        RigidBodyState.Serialize(writer);
    }
}