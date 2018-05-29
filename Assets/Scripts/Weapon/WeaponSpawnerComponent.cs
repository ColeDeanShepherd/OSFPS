using UnityEngine;

public class WeaponSpawnerComponent : MonoBehaviour
{
    public WeaponSpawnerState State;

    private void Start()
    {
        gameObject.GetComponent<MeshRenderer>().enabled = false;
    }
}