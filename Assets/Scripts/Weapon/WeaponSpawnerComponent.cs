using UnityEditor;
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

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.DrawWireSphere(transform.position, 0.25f);
        Handles.Label(transform.position, State.Type.ToString());
    }
#endif
}