using System.IO;

// Server -> Client
public class GameStateMessage : INetworkMessage
{
    public GameState GameState;

    public NetworkMessageType GetMessageType()
    {
        return NetworkMessageType.GameState;
    }

    public void Serialize(BinaryWriter writer)
    {
        GameState.Serialize(writer);
    }
    public void Deserialize(BinaryReader reader)
    {
        GameState = new GameState();
        GameState.Deserialize(reader);
    }
}