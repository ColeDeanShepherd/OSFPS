using UnityEngine;

public class GrenadeComponent : MonoBehaviour
{
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
        Rigidbody = GetComponent<Rigidbody>();
        Collider = GetComponent<Collider>();
    }
    private void OnCollisionEnter(Collision collision)
    {
        GrenadeSystem.Instance.GrenadeOnCollisionEnter(this, collision);
    }
    private void OnCollisionStay(Collision collision)
    {
        var otherGameObject = collision.gameObject;
        var playerObject = otherGameObject.FindObjectOrAncestorWithTag(OsFps.PlayerTag);

        if (playerObject != null)
        {
            PlayerSystem.Instance.OnPlayerCollidingWithGrenade(playerObject, gameObject);
        }
    }
    private void OnDestroy()
    {
        if (State.GrenadeSpawnerId.HasValue)
        {
            var grenadeSpawnerComponent = GrenadeSpawnerSystem.Instance.FindGrenadeSpawnerComponent(State.GrenadeSpawnerId.Value);

            if (grenadeSpawnerComponent != null)
            {
                grenadeSpawnerComponent.State.TimeUntilNextSpawn = GrenadeSystem.Instance.GetGrenadeDefinitionByType(grenadeSpawnerComponent.State.Type).SpawnInterval;
            }
        }
    }
    private void LateUpdate()
    {
        State.RigidBodyState = (Rigidbody != null)
            ? OsFps.ToRigidBodyState(Rigidbody)
            : new RigidBodyState();
    }
    private void ApplyStateFromServer(object newState)
    {
        var newGrenadeState = (GrenadeState)newState;

        OsFps.ApplyRigidbodyState(
            newGrenadeState.RigidBodyState,
            State.RigidBodyState,
            Rigidbody,
            OsFps.Instance.Client.ClientPeer.RoundTripTime ?? 0
        );

        State = newGrenadeState;
    }
}