using UnityEngine;

public class DedicatedServerScreenComponent : MonoBehaviour
{
    public void OnShutdownClick()
    {
        OsFps.Instance.PopMenu();
        OsFps.Instance.ShutdownServer();
    }
}