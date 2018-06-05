using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuComponent : MonoBehaviour
{
    public InputField IpAddressPortInputField;

    public void OnConnectToServerClick()
    {
        OsFps.Instance.EnteredClientIpAddressAndPort = IpAddressPortInputField.text;
        SceneManager.sceneLoaded += OsFps.Instance.OnMapLoadedAsClient;
        SceneManager.LoadScene(OsFps.SmallMapSceneName);
        OsFps.Instance.PopMenu();
    }
    public void OnStartDedicatedServerClick()
    {
        OsFps.Instance.Server = new Server();
        OsFps.Instance.Server.Start();
        OsFps.Instance.PopMenu();
    }
    public void OnStartListenServerClick()
    {
    }
    public void OnOptionsClick()
    {
        OsFps.Instance.PushMenu(OsFps.Instance.CreateOptionsScreen().GetComponent<OptionsScreenComponent>());
    }
    public void OnQuitClick()
    {
        Application.Quit();
    }

    private void Start()
    {
        IpAddressPortInputField.text = OsFps.LocalHostIpv4Address + ":" + Server.PortNumber;
    }
}