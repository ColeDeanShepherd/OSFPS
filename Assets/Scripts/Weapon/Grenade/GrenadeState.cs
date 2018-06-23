using NetworkLibrary;

[System.Serializable]
[NetworkSynchronizedComponent(MonoBehaviourType = typeof(GrenadeComponent))]
public class GrenadeState
{
    public uint Id;
    public GrenadeType Type;
    public RigidBodyState RigidBodyState = new RigidBodyState();
    public bool IsActive;
    public float? TimeUntilDetonation;

    [NotNetworkSynchronized]
    public uint? ThrowerPlayerId;
    [NotNetworkSynchronized]
    public uint? GrenadeSpawnerId;
}