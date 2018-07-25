using System.Collections.Generic;
using UnityEngine;

public class RocketComponent : MonoBehaviour
{
    public static List<RocketComponent> Instances = new List<RocketComponent>();

    public RocketState State;

    public Rigidbody Rigidbody;
    public Collider Collider;

    private void Awake()
    {
        Instances.Add(this);

        Rigidbody = GetComponent<Rigidbody>();
        Collider = GetComponent<Collider>();
    }
    private void OnDestroy()
    {
        Instances.Remove(this);
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
            ? RigidBodyState.FromRigidbody(Rigidbody)
            : new RigidBodyState();
    }
    private void ApplyStateFromServer(object newState)
    {
        var newRocketState = (RocketState)newState;

        Client.ApplyRigidbodyState(
            newRocketState.RigidBodyState,
            State.RigidBodyState,
            Rigidbody,
            OsFps.Instance.Client.ClientPeer.RoundTripTimeInSeconds ?? 0
        );
        
        State = newRocketState;
    }
}