
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

public class OsFps : MonoBehaviour
{
    public const string LocalHostIpv4Address = "127.0.0.1";
    public const int FireMouseButtonNumber = 0;
    public const string PlayerTag = "Player";
    public const string SpawnPointTag = "Respawn";
    public const int MaxPlayerHealth = 100;
    public const int GunShotDamage = 10;
    public const float RespawnTime = 3;
    public const float MuzzleFlashDuration = 0.1f;

    public const float KillPlaneY = -100;

    public static WeaponDefinition PistolDefinition = new WeaponDefinition
    {
        Type = WeaponType.Pistol,
        MaxAmmo = 100,
        BulletsPerMagazine = 10,
        DamagePerBullet = 10,
        ReloadTime = 1
    };
    public static WeaponDefinition GetWeaponDefinitionByType(WeaponType type)
    {
        switch (type)
        {
            case WeaponType.Pistol:
                return PistolDefinition;
            default:
                throw new System.NotImplementedException();
        }
    }

    public static OsFps Instance;
    
    public Server Server;
    public Client Client;

    public GameObject CanvasObject;

    #region Inspector-set Variables
    public GameObject PlayerPrefab;
    public GameObject CameraPrefab;
    public GameObject PistolPrefab;
    public GameObject MuzzleFlashPrefab;

    public GameObject GUIContainerPrefab;
    public GameObject CrosshairPrefab;
    #endregion

    public ConnectionConfig CreateConnectionConfig(
        out int reliableSequencedChannelId,
        out int reliableChannelId,
        out int unreliableStateUpdateChannelId
    )
    {
        var connectionConfig = new ConnectionConfig();
        reliableSequencedChannelId = connectionConfig.AddChannel(QosType.ReliableSequenced);
        reliableChannelId = connectionConfig.AddChannel(QosType.Reliable);
        unreliableStateUpdateChannelId = connectionConfig.AddChannel(QosType.StateUpdate);

        return connectionConfig;
    }

    public GameObject SpawnLocalPlayer(PlayerState playerState)
    {
        var playerObject = Instantiate(
            PlayerPrefab, playerState.Position, Quaternion.Euler(playerState.LookDirAngles)
        );

        var playerComponent = playerObject.GetComponent<PlayerComponent>();
        playerComponent.Id = playerState.Id;
        playerComponent.Rigidbody.velocity = playerState.Velocity;

        return playerObject;
    }
    public GameObject GetWeaponPrefab(WeaponType weaponType)
    {
        switch (weaponType)
        {
            case WeaponType.Pistol:
                return PistolPrefab;
            default:
                throw new System.NotImplementedException();
        }
    }
    public GameObject SpawnLocalWeaponObject(WeaponObjectState weaponObjectState)
    {
        var weaponPrefab = GetWeaponPrefab(weaponObjectState.Type);
        var weaponObject = Instantiate(
            weaponPrefab,
            weaponObjectState.RigidBodyState.Position,
            Quaternion.Euler(weaponObjectState.RigidBodyState.EulerAngles)
        );

        var weaponObjectComponent = weaponObject.GetComponent<WeaponComponent>();
        weaponObjectComponent.Id = weaponObjectState.Id;
        weaponObjectComponent.BulletsLeftInMagazine = weaponObjectState.BulletsLeftInMagazine;
        weaponObjectComponent.BulletsLeftOutOfMagazine = weaponObjectState.BulletsLeftOutOfMagazine;

        return weaponObject;
    }
    public GameObject FindPlayerObject(uint playerId)
    {
        return GameObject.FindGameObjectsWithTag(PlayerTag)
            .FirstOrDefault(go => go.GetComponent<PlayerComponent>().Id == playerId);
    }
    public GameObject FindWeaponObject(uint id)
    {
        var weaponComponent = FindObjectsOfType<WeaponComponent>()
            .FirstOrDefault(wc => wc.Id == id);

        return (weaponComponent != null) ? weaponComponent.gameObject : null;
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
            IsMoveLeftPressed = Input.GetKey(KeyCode.A),
            IsFirePressed = Input.GetMouseButton(FireMouseButtonNumber)
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
    public void UpdatePlayerMovement(PlayerState playerState)
    {
        var playerComponent = FindPlayerComponent(playerState.Id);
        if (playerComponent == null) return;

        ApplyLookDirAnglesToPlayer(playerComponent, playerState.LookDirAngles);

        var relativeMoveDirection = GetRelativeMoveDirection(playerState.Input);
        playerComponent.Rigidbody.AddRelativeForce(10 * relativeMoveDirection);

        // TODO: Handle fire pressed?
    }

    public Vector2 GetPlayerLookDirAngles(PlayerComponent playerComponent)
    {
        return new Vector2(
            playerComponent.CameraPointObject.transform.localEulerAngles.x,
            playerComponent.transform.eulerAngles.y
        );
    }
    public void ApplyLookDirAnglesToPlayer(PlayerComponent playerComponent, Vector2 LookDirAngles)
    {
        playerComponent.transform.localEulerAngles = new Vector3(0, LookDirAngles.y, 0);
        playerComponent.CameraPointObject.transform.localEulerAngles = new Vector3(LookDirAngles.x, 0, 0);
    }

    public void OnPlayerCollidingWithWeapon(GameObject playerObject, GameObject weaponObject)
    {
        if (Server != null)
        {
            Server.OnPlayerCollidingWithWeapon(playerObject, weaponObject);
        }
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

        GameObject guiContainer = Instantiate(GUIContainerPrefab);
        DontDestroyOnLoad(guiContainer);

        CanvasObject = guiContainer.FindDescendant("Canvas");
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
        else
        {
            if (Client != null)
            {
                Client.OnGui();
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