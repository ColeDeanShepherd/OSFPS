using UnityEngine;

public class WeaponComponent : MonoBehaviour
{
    public WeaponObjectState State;
    public WeaponType Type;
    public ushort BulletsLeftInMagazine;
    public ushort BulletsLeftOutOfMagazine;
    public WeaponDefinition Definition
    {
        get
        {
            return OsFps.GetWeaponDefinitionByType(Type);
        }
    }
    public uint? WeaponSpawnerId;

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
        if (WeaponSpawnerId.HasValue)
        {
            var weaponSpawnerComponent = OsFps.Instance.FindWeaponSpawnerComponent(WeaponSpawnerId.Value);

            if (weaponSpawnerComponent != null)
            {
                weaponSpawnerComponent.State.TimeUntilNextSpawn = OsFps.GetWeaponDefinitionByType(weaponSpawnerComponent.WeaponType).SpawnInterval;
            }
        }
    }
}