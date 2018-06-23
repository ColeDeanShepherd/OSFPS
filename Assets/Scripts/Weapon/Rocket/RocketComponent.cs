using UnityEngine;

public class RocketComponent : MonoBehaviour
{
    public RocketState State;

    public Rigidbody Rigidbody;
    public Collider Collider;

    private void Awake()
    {
        Rigidbody = GetComponent<Rigidbody>();
        Collider = GetComponent<Collider>();
    }
    private void Start()
    {
        Destroy(gameObject, OsFps.MaxRocketLifetime);
    }
    private void OnCollisionEnter(Collision collision)
    {
        RocketSystem.Instance.RocketOnCollisionEnter(this, collision);
    }
    private void LateUpdate()
    {
        State.RigidBodyState = (Rigidbody != null)
            ? OsFps.ToRigidBodyState(Rigidbody)
            : new RigidBodyState();
    }
    private void ApplyStateFromServer(object newState)
    {
        var newRocketState = (RocketState)newState;

        OsFps.ApplyRigidbodyState(
            newRocketState.RigidBodyState,
            State.RigidBodyState,
            Rigidbody,
            OsFps.Instance.Client.ClientPeer.RoundTripTime ?? 0
        );
        
        State = newRocketState;
    }
}