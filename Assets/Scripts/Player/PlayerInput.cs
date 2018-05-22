using System.IO;

public struct PlayerInput : INetworkSerializable
{
    public bool IsMoveFowardPressed;
    public bool IsMoveBackwardPressed;
    public bool IsMoveRightPressed;
    public bool IsMoveLeftPressed;
    public bool IsFirePressed;

    public void Serialize(BinaryWriter writer)
    {
        writer.Write(IsMoveFowardPressed);
        writer.Write(IsMoveBackwardPressed);
        writer.Write(IsMoveRightPressed);
        writer.Write(IsMoveLeftPressed);
        writer.Write(IsFirePressed);
    }
    public void Deserialize(BinaryReader reader)
    {
        IsMoveFowardPressed = reader.ReadBoolean();
        IsMoveBackwardPressed = reader.ReadBoolean();
        IsMoveRightPressed = reader.ReadBoolean();
        IsMoveLeftPressed = reader.ReadBoolean();
        IsFirePressed = reader.ReadBoolean();
    }
}