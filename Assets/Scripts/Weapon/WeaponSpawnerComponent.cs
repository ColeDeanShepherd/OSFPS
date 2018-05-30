using UnityEngine;

public class WeaponSpawnerComponent : MonoBehaviour
{
    public WeaponSpawnerState State;

    private void Awake()
    {
        if (OsFps.Instance.IsServer && (State.Id == 0))
        {
            State.Id = OsFps.Instance.Server.GenerateNetworkId();
            State.TimeUntilNextSpawn = 0;
        }
    }
    private void Start()
    {
        gameObject.GetComponent<MeshRenderer>().enabled = false;
    }
}