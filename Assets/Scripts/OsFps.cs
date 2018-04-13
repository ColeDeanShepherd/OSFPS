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
        playerObject.GetComponent<PlayerComponent>().Id = playerState.Id;

        return playerObject;
    }
    public GameObject FindPlayerObject(uint playerId)
    {
        return GameObject.FindGameObjectsWithTag("Player")
            .FirstOrDefault(go => go.GetComponent<PlayerComponent>().Id == playerId);
    }
    public PlayerComponent FindPlayerComponent(uint playerId)
    {
        var playerObject = FindPlayerObject(playerId);
        return (playerObject != null) ? playerObject.GetComponent<PlayerComponent>() : null;
    }
    
    public PlayerInput GetCurrentPlayersInput()
    {
        return new PlayerInput
        {
            IsMoveFowardPressed = Input.GetKey(KeyCode.W),
            IsMoveBackwardPressed = Input.GetKey(KeyCode.S),
            IsMoveRightPressed = Input.GetKey(KeyCode.D),
            IsMoveLeftPressed = Input.GetKey(KeyCode.A)
        };
    }
    public Vector3 GetRelativeMoveDirection(PlayerInput input)
    {
        var moveDirection = Vector3.zero;

        if (input.IsMoveFowardPressed)
        {
            moveDirection += Vector3.forward;
        }

        if (input.IsMoveBackwardPressed)
        {
            moveDirection += Vector3.back;
        }

        if (input.IsMoveRightPressed)
        {
            moveDirection += Vector3.right;
        }

        if (input.IsMoveLeftPressed)
        {
            moveDirection += Vector3.left;
        }

        return moveDirection.normalized;
    }
    public void UpdatePlayer(PlayerState playerState)
    {
        var playerComponent = FindPlayerComponent(playerState.Id);

        ApplyEulerAnglesToPlayer(playerComponent, playerState.EulerAngles);

        var relativeMoveDirection = GetRelativeMoveDirection(playerState.Input);
        playerComponent.Rigidbody.AddRelativeForce(10 * relativeMoveDirection);
    }

    public Vector3 GetPlayerEulerAngles(PlayerComponent playerComponent)
    {
        return new Vector3(
            playerComponent.CameraPointObject.transform.localEulerAngles.x,
            playerComponent.transform.eulerAngles.y,
            0
        );
    }
    public void ApplyEulerAnglesToPlayer(PlayerComponent playerComponent, Vector3 eulerAngles)
    {
        playerComponent.transform.localEulerAngles = new Vector3(0, eulerAngles.y, 0);
        playerComponent.CameraPointObject.transform.localEulerAngles = new Vector3(eulerAngles.x, 0, 0);
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
        ShutdownNetworkPeers();
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
    private void LateUpdate()
    {
        if (Server != null)
        {
            Server.LateUpdate();
        }

        if (Client != null)
        {
            Client.LateUpdate();
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
                Server.Start();
            }
        }
    }

    private void ShutdownNetworkPeers()
    {
        if (Client != null)
        {
            Client.DisconnectFromServer();
            Client.Stop();

            Client = null;
        }

        if (Server != null)
        {
            Server.Stop();

            Server = null;
        }
    }

    private void OnMapLoadedAsClient(Scene scene, LoadSceneMode loadSceneMode)
    {
        SceneManager.sceneLoaded -= OnMapLoadedAsClient;

        Client = new Client();
        Client.OnDisconnectedFromServer += OnClientDisconnectedFromServer;
        Client.Start(true);
        Client.StartConnectingToServer(LocalHostIpv4Address, Server.PortNumber);
    }
    private void OnClientDisconnectedFromServer()
    {
        ShutdownNetworkPeers();
        SceneManager.LoadScene("Start");
    }
}