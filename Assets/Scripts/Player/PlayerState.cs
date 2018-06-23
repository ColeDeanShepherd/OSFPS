using NetworkLibrary;

[System.Serializable]
[NetworkSynchronizedComponent(MonoBehaviourType = typeof(PlayerComponent))]
public class PlayerState
{
    public uint Id;
    public string Name;
    public short Kills;
    public ushort Deaths;
    public float RespawnTimeLeft;
}