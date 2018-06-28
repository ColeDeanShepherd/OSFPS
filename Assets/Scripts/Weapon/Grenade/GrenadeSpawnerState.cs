using NetworkLibrary;

[System.Serializable]
[NetworkedComponent(MonoBehaviourType = typeof(GrenadeSpawnerComponent))]
public class GrenadeSpawnerState
{
    public uint Id;
    public GrenadeType Type;
    public float? TimeUntilNextSpawn;
}