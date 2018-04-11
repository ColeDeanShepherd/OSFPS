public interface INetworkMessage : INetworkSerializable
{
    NetworkMessageType GetMessageType();
}