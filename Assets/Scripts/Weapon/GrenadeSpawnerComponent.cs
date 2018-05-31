using UnityEngine;

public class GrenadeSpawnerComponent : MonoBehaviour
{
    public GrenadeSpawnerState State;

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