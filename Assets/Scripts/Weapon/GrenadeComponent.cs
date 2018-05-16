using UnityEngine;

public class GrenadeComponent : MonoBehaviour
{
    public uint Id;
    public GrenadeType Type;
    public GrenadeDefinition Definition
    {
        get
        {
            return OsFps.GetGrenadeDefinitionByType(Type);
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
            OsFps.Instance.OnPlayerCollidingWithGrenade(playerObject, gameObject);
        }
    }
}