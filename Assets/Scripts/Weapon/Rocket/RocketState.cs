[System.Serializable]
[NetworkSynchronizedComponent(MonoBehaviourType = typeof(RocketComponent))]
public class RocketState
{
    public uint Id;
    public RigidBodyState RigidBodyState = new RigidBodyState();

    [NotNetworkSynchronized]
    public uint? ShooterPlayerId;
}