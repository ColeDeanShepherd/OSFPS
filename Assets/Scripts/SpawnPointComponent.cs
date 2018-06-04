using UnityEditor;
using UnityEngine;

public class SpawnPointComponent : MonoBehaviour
{
#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.DrawWireCube(transform.position, 0.25f * Vector3.one);
        Handles.Label(transform.position, "SpawnPoint");
    }
#endif
}