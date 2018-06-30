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
            return WeaponObjectSystem.Instance.GetWeaponDefinitionByType(State.Type);
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
        var otherGameObject = collision.gameObject;
        var playerObject = otherGameObject.FindObjectOrAncestorWithTag(OsFps.PlayerTag);

        if (playerObject != null)
        {
            PlayerObjectSystem.Instance.OnPlayerCollidingWithWeapon(playerObject, gameObject);
        }
    }
    private void OnDestroy()
    {
        Instances.Remove(this);

        if (State.WeaponSpawnerId.HasValue)
        {
            var weaponSpawnerComponent = WeaponSpawnerSystem.Instance.FindWeaponSpawnerComponent(State.WeaponSpawnerId.Value);

            if (weaponSpawnerComponent != null)
            {
                weaponSpawnerComponent.State.TimeUntilNextSpawn = WeaponObjectSystem.Instance.GetWeaponDefinitionByType(weaponSpawnerComponent.State.Type).SpawnInterval;
            }
        }
    }
    private void LateUpdate()
    {
        if (State != null)
        {
            State.RigidBodyState = (Rigidbody != null)
                ? OsFps.ToRigidBodyState(Rigidbody)
                : new RigidBodyState();
        }
    }
    private void ApplyStateFromServer(object newState)
    {
        var newWeaponObjectState = (WeaponObjectState)newState;

        OsFps.ApplyRigidbodyState(
            newWeaponObjectState.RigidBodyState,
            State.RigidBodyState,
            Rigidbody,
            OsFps.Instance.Client.ClientPeer.RoundTripTime ?? 0
        );

        State = newWeaponObjectState;
    }
}