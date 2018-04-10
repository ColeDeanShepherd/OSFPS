using System.Linq;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

public class OsFps : MonoBehaviour
{
    public const string LocalHostIpv4Address = "127.0.0.1";

    public static OsFps Instance;
    
    public Server Server;
    public Client Client;

    public uint? CurrentPlayerId
    {
        get
        {
            return (Client != null) ? Client.playerId : (uint?)null;
        }
    }

    // inspector-set variables
    public GameObject PlayerPrefab;
    public GameObject CameraPrefab;

    public GameObject SpawnLocalPlayer(RemoteClientInfo clientInfo)
    {
        var playerObject = Instantiate(PlayerPrefab);

        clientInfo.GameObject = playerObject;

        var playerComponent = playerObject.GetComponent<PlayerComponent>();
        playerComponent.ClientInfo = clientInfo;

        return playerObject;
    }
    public GameObject FindPlayerObject(uint playerId)
    {
        return GameObject.FindGameObjectsWithTag("Player")
            .FirstOrDefault(go => go.GetComponent<PlayerComponent>().ClientInfo.PlayerId == playerId);
    }

    private void Awake()
    {
        // Destroy the game object if there is already an OsFps instance.
        if(Instance != null)
        {
            enabled = false;
            gameObject.SetActive(false);
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }
    private void Start()
    {
        // Initialize & configure network.
        NetworkTransport.Init();
    }
    private void OnDestroy()
    {
        if (Client != null)
        {
            Client.DisconnectFromServer();
            Client.Stop();

            Client = null;
        }

        if (Server != null)
        {
            Server = null;
        }
        
        NetworkTransport.Shutdown();
    }
    private void Update()
    {
        if (Server != null)
        {
            Server.Update();
        }

        if (Client != null)
        {
            Client.Update();
        }
    }
    private void OnGUI()
    {
        if((Server == null) && (Client == null))
        {
            if(GUI.Button(new Rect(10, 10, 200, 30), "Connect To Server"))
            {
                SceneManager.sceneLoaded += OnMapLoadedAsClient;
                SceneManager.LoadScene("Test Map");
            }

            if (GUI.Button(new Rect(10, 50, 200, 30), "Start Server"))
            {
                Server = new Server();
                Server.OnServerStarted += () => {
                    Client = new Client();
                    Client.Start(false);

                    Client.StartConnectingToServer(LocalHostIpv4Address, Server.PortNumber);
                };

                Server.Start();
            }
        }
    }

    private void OnMapLoadedAsClient(Scene scene, LoadSceneMode loadSceneMode)
    {
        SceneManager.sceneLoaded -= OnMapLoadedAsClient;

        Client = new Client();
        Client.Start(true);
        Client.StartConnectingToServer(LocalHostIpv4Address, Server.PortNumber);
    }
}