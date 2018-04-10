using System.IO;

public struct PlayerInput
{
    public bool IsMoveFowardPressed;
    public bool IsMoveBackwardPressed;
    public bool IsMoveRightPressed;
    public bool IsMoveLeftPressed;
    public float XAngle;
    public float YAngle;

    public void Serialize(BinaryWriter writer)
    {
        writer.Write(IsMoveFowardPressed);
        writer.Write(IsMoveBackwardPressed);
        writer.Write(IsMoveRightPressed);
        writer.Write(IsMoveLeftPressed);
        writer.Write(XAngle);
        writer.Write(YAngle);
    }
    public void Deserialize(BinaryReader reader)
    {
        IsMoveFowardPressed = reader.ReadBoolean();
        IsMoveBackwardPressed = reader.ReadBoolean();
        IsMoveRightPressed = reader.ReadBoolean();
        IsMoveLeftPressed = reader.ReadBoolean();
        XAngle = reader.ReadSingle();
        YAngle = reader.ReadSingle();
    }
}