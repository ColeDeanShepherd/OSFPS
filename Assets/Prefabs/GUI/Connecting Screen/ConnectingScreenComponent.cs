using UnityEngine;

public class ConnectingScreenComponent : MonoBehaviour
{
    public void OnCancelClick()
    {
        OsFps.Instance.MenuStack.Pop();
        OsFps.Instance.Client.InternalOnDisconnectedFromServer();
    }
}