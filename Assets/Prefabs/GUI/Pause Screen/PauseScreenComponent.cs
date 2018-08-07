using UnityEngine;

public class PauseScreenComponent : MonoBehaviour
{
    public void OnExitMenuClick()
    {
        OsFps.Instance.MenuStack.Pop();
    }
    public void OnOptionsClick()
    {
        OsFps.Instance.EnterOptionsScreen();
    }
    public void OnLeaveServerClick()
    {
        OsFps.Instance.MenuStack.Pop();
        OsFps.Instance.Client.LeaveServer();
    }
}