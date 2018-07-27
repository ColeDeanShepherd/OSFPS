using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using NetworkLibrary;

public class MainMenuComponent : MonoBehaviour
{
    public InputField IpAddressPortInputField;

    public void OnConnectToServerClick()
    {
        OsFps.Instance.EnteredClientIpAddressAndPort = IpAddressPortInputField.text;
        SceneManager.sceneLoaded += OsFps.Instance.OnMapLoadedAsClient;
        SceneManager.LoadScene(OsFps.SmallMapSceneName);
        OsFps.Instance.MenuStack.Pop();
    }
    public void OnStartDedicatedServerClick()
    {
        OsFps.Instance.Server = new Server();
        OsFps.Instance.Server.Start();
        OsFps.Instance.MenuStack.Pop();
    }
    public void OnStartListenServerClick()
    {
        OsFps.Instance.EnteredClientIpAddressAndPort = NetLib.LocalHostIpv4Address + ":" + Server.PortNumber;
        OsFps.Instance.Server = new Server();
        OsFps.Instance.Server.Start();

        SceneManager.sceneLoaded += OsFps.Instance.OnMapLoadedAsClient;

        OsFps.Instance.MenuStack.Pop();
    }
    public void OnOptionsClick()
    {
        OsFps.Instance.MenuStack.Push(OsFps.Instance.CreateOptionsScreen().GetComponent<OptionsScreenComponent>());
    }
    public void OnWebsiteClick()
    {
        System.Diagnostics.Process.Start("https://github.com/ColeDeanShepherd/OSFPS");
    }
    public void OnQuitClick()
    {
        Application.Quit();
    }

    private void Start()
    {
        IpAddressPortInputField.text = NetLib.LocalHostIpv4Address + ":" + Server.PortNumber;
    }
}