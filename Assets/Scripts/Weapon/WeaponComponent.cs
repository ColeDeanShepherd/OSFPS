using UnityEngine;

public class WeaponComponent : MonoBehaviour
{
    public WeaponType Type;
    public WeaponDefinition Definition
    {
        get
        {
            return OsFps.GetWeaponDefinitionByType(Type);
        }
    }

    public Rigidbody Rigidbody;
    public Collider Collider;

    private void Awake()
    {
        Rigidbody = GetComponent<Rigidbody>();
        Collider = GetComponent<Collider>();
    }
}