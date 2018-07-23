using UnityEngine;

public class PauseScreenComponent : MonoBehaviour
{
    public void OnExitMenuClick()
    {
        OsFps.Instance.PopMenu();
    }
    public void OnOptionsClick()
    {
        OsFps.Instance.PushMenu(OsFps.Instance.CreateOptionsScreen().GetComponent<OptionsScreenComponent>());
    }
    public void OnLeaveServerClick()
    {
        OsFps.Instance.PopMenu();
        OsFps.Instance.Client.LeaveServer();
    }
}