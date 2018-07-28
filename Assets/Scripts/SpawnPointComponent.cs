using UnityEditor;
using UnityEngine;

public class SpawnPointComponent : MonoBehaviour
{
#if UNITY_EDITOR
    private GUIStyle labelStyle;
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(transform.position, 0.25f * Vector3.one);
        Gizmos.color = Color.white;

        if (labelStyle == null)
        {
            labelStyle = new GUIStyle();
            labelStyle.normal.textColor = Color.red;
        }
        Handles.Label(transform.position, "SpawnPoint", labelStyle);
    }
#endif
}