using UnityEngine;

public class GrenadeComponent : MonoBehaviour
{
    public GrenadeState State;
    public uint? ThrowerPlayerId;

    public GrenadeDefinition Definition
    {
        get
        {
            return OsFps.GetGrenadeDefinitionByType(State.Type);
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
        OsFps.Instance.GrenadeOnCollisionEnter(this, collision);
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