using UnityEngine;

public class ConnectingScreenComponent : MonoBehaviour
{
    public void OnCancelClick()
    {
        OsFps.Instance.PopMenu();
        OsFps.Instance.Client.InternalOnDisconnectedFromServer();
    }
}