using UnityEngine;

public class DedicatedServerScreenComponent : MonoBehaviour
{
    public void OnShutdownClick()
    {
        OsFps.Instance.MenuStack.Pop();
        OsFps.Instance.ShutdownServer();
    }
}