using System.Collections.Generic;
using UnityEngine;

public class WeaponComponent : MonoBehaviour
{
    public static List<WeaponComponent> Instances = new List<WeaponComponent>();

    public WeaponObjectState State;
    public WeaponDefinition Definition
    {
        get
        {
            return WeaponSystem.Instance.GetWeaponDefinitionByType(State.Type);
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
    private void OnCollisionStay(Collision collision)
    {
        WeaponSystem.Instance.WeaponOnCollisionStay(this, collision);
    }
    private void OnDestroy()
    {
        Instances.Remove(this);
        WeaponSystem.Instance.WeaponOnDestroy(this);
    }
    private void LateUpdate()
    {
        if (State != null)
        {
            State.RigidBodyState = (Rigidbody != null)
                ? RigidBodyState.FromRigidbody(Rigidbody)
                : new RigidBodyState();
        }
    }
    private void ApplyStateFromServer(object newState)
    {
        var newWeaponObjectState = (WeaponObjectState)newState;

        Client.ApplyRigidbodyState(
            newWeaponObjectState.RigidBodyState,
            State.RigidBodyState,
            Rigidbody,
            OsFps.Instance.Client.ClientPeer.RoundTripTimeInSeconds ?? 0
        );

        State = newWeaponObjectState;
    }
}