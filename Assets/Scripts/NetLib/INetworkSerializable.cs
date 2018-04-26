using System.IO;

public interface INetworkSerializable
{
    void Serialize(BinaryWriter writer);
    void Deserialize(BinaryReader reader);
}