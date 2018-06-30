using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class WeaponSpawnerComponent : MonoBehaviour
{
    public static List<WeaponSpawnerComponent> Instances = new List<WeaponSpawnerComponent>();

    public WeaponSpawnerState State;

    private void Awake()
    {
        Instances.Add(this);

        if (OsFps.Instance.IsServer && (State.Id == 0))
        {
            State.Id = OsFps.Instance.Server.GenerateNetworkId();
            State.TimeUntilNextSpawn = 0;
        }
    }
    private void OnDestroy()
    {
        Instances.Remove(this);
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.DrawWireSphere(transform.position, 0.25f);
        Handles.Label(transform.position, State.Type.ToString());
    }
#endif
}