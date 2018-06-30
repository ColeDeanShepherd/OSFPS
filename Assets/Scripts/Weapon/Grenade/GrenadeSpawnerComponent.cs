using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class GrenadeSpawnerComponent : MonoBehaviour
{
    public static List<GrenadeSpawnerComponent> Instances = new List<GrenadeSpawnerComponent>();

    public GrenadeSpawnerState State;

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