using System.IO;

namespace NetworkLibrary
{
    public interface INetworkSerializable
    {
        void Serialize(BinaryWriter writer);
        void Deserialize(BinaryReader reader);
    }
}