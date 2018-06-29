using NetworkLibrary;

[System.Serializable]
[NetworkedComponent(MonoBehaviourType = typeof(WeaponSpawnerComponent))]
public class WeaponSpawnerState
{
    public uint Id;
    public WeaponType Type;
    public float? TimeUntilNextSpawn;
}