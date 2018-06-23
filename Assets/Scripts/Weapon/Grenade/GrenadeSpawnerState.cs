using NetworkLibrary;

[System.Serializable]
[NetworkSynchronizedComponent(MonoBehaviourType = typeof(GrenadeSpawnerComponent))]
public class GrenadeSpawnerState
{
    public uint Id;
    public GrenadeType Type;
    public float? TimeUntilNextSpawn;
}