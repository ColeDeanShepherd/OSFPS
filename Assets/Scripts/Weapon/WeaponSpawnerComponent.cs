using UnityEngine;

public class WeaponSpawnerComponent : MonoBehaviour
{
    public WeaponType WeaponType;
    public WeaponSpawnerState State;

    private void Start()
    {
        gameObject.GetComponent<MeshRenderer>().enabled = false;
    }
}