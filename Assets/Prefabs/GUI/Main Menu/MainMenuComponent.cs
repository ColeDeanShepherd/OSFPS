using UnityEngine;
using UnityEngine.UI;
using NetworkLibrary;

public class MainMenuComponent : MonoBehaviour
{
    public InputField IpAddressPortInputField;

    public void OnMatchmakingClick()
    {
        OsFps.Instance.EnterMatchmakingScreen();
    }
    public void OnConnectToServerClick()
    {
        OsFps.Instance.ConnectToServer(IpAddressPortInputField.text);
    }
    public void OnStartDedicatedServerClick()
    {
        OsFps.Instance.StartDedicatedServer();
    }
    public void OnStartListenServerClick()
    {
        OsFps.Instance.StartListenServer();
    }
    public void OnOptionsClick()
    {
        OsFps.Instance.EnterOptionsScreen();
    }
    public void OnWebsiteClick()
    {
        OpenUrlInDefaultBrowser("https://github.com/ColeDeanShepherd/OSFPS");
    }
    public void OnQuitClick()
    {
        Application.Quit();
    }

    private void Start()
    {
        IpAddressPortInputField.text = NetLib.LocalHostIpv4Address + ":" + Server.PortNumber;
    }
    private void OpenUrlInDefaultBrowser(string url)
    {
        System.Diagnostics.Process.Start(url);
    }
}