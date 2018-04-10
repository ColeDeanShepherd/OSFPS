using System.Linq;
using UnityEngine;
using UnityEngine.Networking;

public class OsFps : MonoBehaviour
{
    public const string LocalHostIpv4Address = "127.0.0.1";

    public static OsFps Instance;
    
    public Server Server;
    public Client Client;

    // inspector-set variables
    public GameObject PlayerPrefab;
    public GameObject CameraPrefab;

    public GameObject SpawnLocalPlayer(RemoteClientInfo clientInfo)
    {
        var playerObject = Instantiate(PlayerPrefab);

        clientInfo.GameObject = playerObject;

        var playerComponent = playerObject.GetComponent<PlayerComponent>();
        playerComponent.clientInfo = clientInfo;

        return playerObject;
    }
    public GameObject FindPlayerObject(uint playerId)
    {
        return GameObject.FindGameObjectsWithTag("Player")
            .FirstOrDefault(go => go.GetComponent<PlayerComponent>().clientInfo.PlayerId == playerId);
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

        Server = new Server();
        Server.OnServerStarted += () => {
            Client = new Client();
            Client.Start(false);

            Client.StartConnectingToServer(LocalHostIpv4Address, Server.PortNumber);
        };

        Server.Start();
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
}