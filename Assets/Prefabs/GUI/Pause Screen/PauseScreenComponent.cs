using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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
        OsFps.Instance.Client.LeaveServer();
        OsFps.Instance.PopMenu();
    }
}