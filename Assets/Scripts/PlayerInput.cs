using System.IO;

public struct PlayerInput
{
    public bool IsMoveFowardPressed;
    public bool IsMoveBackwardPressed;
    public bool IsMoveRightPressed;
    public bool IsMoveLeftPressed;

    public void Serialize(BinaryWriter writer)
    {
        writer.Write(IsMoveFowardPressed);
        writer.Write(IsMoveBackwardPressed);
        writer.Write(IsMoveRightPressed);
        writer.Write(IsMoveLeftPressed);
    }
    public void Deserialize(BinaryReader reader)
    {
        IsMoveFowardPressed = reader.ReadBoolean();
        IsMoveBackwardPressed = reader.ReadBoolean();
        IsMoveRightPressed = reader.ReadBoolean();
        IsMoveLeftPressed = reader.ReadBoolean();
    }
}