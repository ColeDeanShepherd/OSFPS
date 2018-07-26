using System.Collections.Generic;
using UnityEngine;

public class GrenadeComponent : MonoBehaviour
{
    public static List<GrenadeComponent> Instances = new List<GrenadeComponent>();

    public GrenadeState State;

    public GrenadeDefinition Definition
    {
        get
        {
            return GrenadeSystem.Instance.GetGrenadeDefinitionByType(State.Type);
        }
    }

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
        GrenadeSystem.Instance.GrenadeOnDestroy(this);
    }

    private void OnCollisionEnter(Collision collision)
    {
        GrenadeSystem.Instance.GrenadeOnCollisionEnter(this, collision);
    }
    private void OnCollisionStay(Collision collision)
    {
        GrenadeSystem.Instance.GrenadeOnCollisionStay(this, collision);
    }
    private void LateUpdate()
    {
        State.RigidBodyState = (Rigidbody != null)
            ? RigidBodyState.FromRigidbody(Rigidbody)
            : new RigidBodyState();
    }
    private void ApplyStateFromServer(object newState)
    {
        var newGrenadeState = (GrenadeState)newState;

        Client.ApplyRigidbodyState(
            newGrenadeState.RigidBodyState,
            State.RigidBodyState,
            Rigidbody,
            OsFps.Instance.Client.ClientPeer.RoundTripTimeInSeconds ?? 0
        );

        State = newGrenadeState;
    }
}