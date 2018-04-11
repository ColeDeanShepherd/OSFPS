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
            return (Client != null) ? Client.PlayerId : (uint?)null;
        }
    }

    // inspector-set variables
    public GameObject PlayerPrefab;
    public GameObject CameraPrefab;

    public ConnectionConfig CreateConnectionConfig(
        out int reliableSequencedChannelId,
        out int unreliableStateUpdateChannelId
    )
    {
        var connectionConfig = new ConnectionConfig();
        reliableSequencedChannelId = connectionConfig.AddChannel(QosType.ReliableSequenced);
        unreliableStateUpdateChannelId = connectionConfig.AddChannel(QosType.StateUpdate);

        return connectionConfig;
    }

    public GameObject SpawnLocalPlayer(PlayerState playerState)
    {
        var playerObject = Instantiate(
            PlayerPrefab, playerState.Position, Quaternion.Euler(playerState.EulerAngles)
        );
        var playerComponent = playerObject.GetComponent<PlayerComponent>();

        playerComponent.State = playerState;
        
        return playerObject;
    }
    public GameObject FindPlayerObject(uint playerId)
    {
        return GameObject.FindGameObjectsWithTag("Player")
            .FirstOrDefault(go => go.GetComponent<PlayerComponent>().State.Id == playerId);
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