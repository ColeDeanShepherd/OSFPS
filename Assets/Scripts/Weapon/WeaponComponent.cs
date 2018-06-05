using UnityEngine;

public class WeaponComponent : MonoBehaviour
{
    public WeaponObjectState State;
    public WeaponDefinition Definition
    {
        get
        {
            return OsFps.Instance.GetWeaponDefinitionByType(State.Type);
        }
    }

    public Rigidbody Rigidbody;
    public Collider Collider;

    private void Awake()
    {
        Rigidbody = GetComponent<Rigidbody>();
        Collider = GetComponent<Collider>();
    }
    private void OnCollisionStay(Collision collision)
    {
        var otherGameObject = collision.gameObject;
        var playerObject = otherGameObject.FindObjectOrAncestorWithTag(OsFps.PlayerTag);

        if (playerObject != null)
        {
            OsFps.Instance.OnPlayerCollidingWithWeapon(playerObject, gameObject);
        }
    }
    private void OnDestroy()
    {
        if (State.WeaponSpawnerId.HasValue)
        {
            var weaponSpawnerComponent = OsFps.Instance.FindWeaponSpawnerComponent(State.WeaponSpawnerId.Value);

            if (weaponSpawnerComponent != null)
            {
                weaponSpawnerComponent.State.TimeUntilNextSpawn = OsFps.Instance.GetWeaponDefinitionByType(weaponSpawnerComponent.State.Type).SpawnInterval;
            }
        }
    }
}