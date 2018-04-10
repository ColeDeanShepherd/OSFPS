using System.IO;
using UnityEngine.Networking;

public abstract class NetworkMessage
{
    public abstract NetworkMessageType GetMessageType();
    public byte[] Serialize()
    {
        var memoryStream = new MemoryStream();
        var writer = new BinaryWriter(memoryStream);
        writer.Write((byte)GetMessageType());
        SerializeWithoutType(writer);

        return memoryStream.ToArray();
    }
    public void Deserialize(BinaryReader reader)
    {
        DeserializeWithoutType(reader);
    }

    protected abstract void SerializeWithoutType(BinaryWriter writer);
    protected abstract void DeserializeWithoutType(BinaryReader reader);
}