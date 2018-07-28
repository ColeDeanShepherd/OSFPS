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
    private GUIStyle labelStyle;
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, 0.25f);
        Gizmos.color = Color.white;

        if (labelStyle == null)
        {
            labelStyle = new GUIStyle();
            labelStyle.normal.textColor = Color.red;
        }
        Handles.Label(transform.position, State.Type.ToString(), labelStyle);
    }
#endif
}