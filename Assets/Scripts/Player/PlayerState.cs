using NetworkLibrary;

[System.Serializable]
[NetworkedComponent(MonoBehaviourType = typeof(PlayerComponent))]
public class PlayerState
{
    public uint Id;
    public string Name;
    public short Kills;
    public ushort Deaths;
    public float RespawnTimeLeft;
}