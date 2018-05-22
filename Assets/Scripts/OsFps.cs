using System.Collections.Generic;
using System.Linq;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

/*
TODO
====
-Handle networked shot intervals better.
-Improve server-side verification (use round trip time).
-Send shot rays from client.
-Set is fire pressed to false when switching weapons.
-Fix other players reloading.
-Improve player movement.
-Add player head & body.
-Add shields.
-Attribute grenade kills to the correct player.
-Improve grenade trajectories.
-Don't add bullets to magazine when running over weapon.
-Implement delta game state sending.
-Make weapons spawn repeatedly.
-Remove system instances.
-Init network IDs for objects placed in scene.
*/

public class OsFps : MonoBehaviour
{
    public const string LocalHostIpv4Address = "127.0.0.1";

    public const string PlayerTag = "Player";
    public const string SpawnPointTag = "Respawn";

    public const int MaxPlayerHealth = 100;
    public const float RespawnTime = 3;

    public const float MuzzleFlashDuration = 0.1f;
    public const int MaxWeaponCount = 2;

    public const int MaxGrenadesPerType = 2;
    public const float GrenadeThrowInterval = 1;
    public const float GrenadeThrowSpeed = 10;
    public const float GrenadeExplosionForce = 500;
    public const float GrenadeExplosionDuration = 0.5f;

    public const int FireMouseButtonNumber = 0;
    public const KeyCode MoveForwardKeyCode = KeyCode.W;
    public const KeyCode MoveBackwardKeyCode = KeyCode.S;
    public const KeyCode MoveRightKeyCode = KeyCode.D;
    public const KeyCode MoveLeftKeyCode = KeyCode.A;
    public const KeyCode ReloadKeyCode = KeyCode.R;
    public const KeyCode ThrowGrenadeKeyCode = KeyCode.G;
    public const KeyCode ShowScoreboardKeyCode = KeyCode.Tab;
    public const KeyCode ChatKeyCode = KeyCode.Return;
    public const KeyCode ToggleMenuKeyCode = KeyCode.Escape;

    public const float KillPlaneY = -100;

    public static WeaponDefinition PistolDefinition = new WeaponDefinition
    {
        Type = WeaponType.Pistol,
        MaxAmmo = 100,
        BulletsPerMagazine = 10,
        DamagePerBullet = 10,
        ReloadTime = 1,
        ShotInterval = 0.4f,
        IsAutomatic = false,
        SpawnInterval = 10
    };
    public static WeaponDefinition SmgDefinition = new WeaponDefinition
    {
        Type = WeaponType.Smg,
        MaxAmmo = 100,
        BulletsPerMagazine = 10,
        DamagePerBullet = 10,
        ReloadTime = 1,
        ShotInterval = 0.1f,
        IsAutomatic = true,
        SpawnInterval = 20
    };
    public static WeaponDefinition GetWeaponDefinitionByType(WeaponType type)
    {
        switch (type)
        {
            case WeaponType.Pistol:
                return PistolDefinition;
            case WeaponType.Smg:
                return SmgDefinition;
            default:
                throw new System.NotImplementedException();
        }
    }

    public static GrenadeDefinition FragmentationGrenadeDefinition = new GrenadeDefinition
    {
        Type = GrenadeType.Fragmentation,
        Damage = 90,
        TimeAfterHitUntilDetonation = 1,
        ExplosionRadius = 4,
        SpawnInterval = 20
    };
    public static GrenadeDefinition StickyGrenadeDefinition = new GrenadeDefinition
    {
        Type = GrenadeType.Sticky,
        Damage = 90,
        TimeAfterHitUntilDetonation = 1,
        ExplosionRadius = 4,
        SpawnInterval = 20
    };
    public static GrenadeDefinition GetGrenadeDefinitionByType(GrenadeType type)
    {
        switch (type)
        {
            case GrenadeType.Fragmentation:
                return FragmentationGrenadeDefinition;
            case GrenadeType.Sticky:
                return StickyGrenadeDefinition;
            default:
                throw new System.NotImplementedException();
        }
    }

    public static OsFps Instance;
    
    public Server Server;
    public Client Client;

    [HideInInspector]
    public GameObject CanvasObject;

    #region Inspector-set Variables
    public GameObject PlayerPrefab;
    public GameObject CameraPrefab;

    public GameObject PistolPrefab;
    public GameObject SmgPrefab;

    public GameObject MuzzleFlashPrefab;

    public GameObject FragmentationGrenadePrefab;
    public GameObject FragmentationGrenadeExplosionPrefab;

    public GameObject StickyGrenadePrefab;
    public GameObject StickyGrenadeExplosionPrefab;

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

    public GameObject GetWeaponPrefab(WeaponType weaponType)
    {
        switch (weaponType)
        {
            case WeaponType.Pistol:
                return PistolPrefab;
            case WeaponType.Smg:
                return SmgPrefab;
            default:
                throw new System.NotImplementedException("Unknown weapon type: " + weaponType);
        }
    }
    public GameObject GetGrenadePrefab(GrenadeType grenadeType)
    {
        switch (grenadeType)
        {
            case GrenadeType.Fragmentation:
                return FragmentationGrenadePrefab;
            case GrenadeType.Sticky:
                return StickyGrenadePrefab;
            default:
                throw new System.NotImplementedException("Unknown grenade type: " + grenadeType);
        }
    }
    public GameObject GetGrenadeExplosionPrefab(GrenadeType grenadeType)
    {
        switch (grenadeType)
        {
            case GrenadeType.Fragmentation:
                return FragmentationGrenadeExplosionPrefab;
            case GrenadeType.Sticky:
                return StickyGrenadeExplosionPrefab;
            default:
                throw new System.NotImplementedException("Unknown grenade type: " + grenadeType);
        }
    }

    public GameObject CreateLocalPlayerDataObject(PlayerState playerState)
    {
        var playerDataObject = new GameObject($"Player {playerState.Id}");

        var playerComponent = playerDataObject.AddComponent<PlayerComponent>();
        playerComponent.State = playerState;

        playerDataObject.AddComponent<GameObjectEntity>();

        return playerDataObject;
    }

    public GameObject SpawnLocalPlayer(PlayerObjectState playerObjectState)
    {
        var playerObject = Instantiate(
            PlayerPrefab, playerObjectState.Position, Quaternion.Euler(playerObjectState.LookDirAngles)
        );

        var playerComponent = playerObject.GetComponent<PlayerObjectComponent>();
        playerComponent.State = playerObjectState;
        playerComponent.Rigidbody.velocity = playerObjectState.Velocity;

        return playerObject;
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
        weaponObjectComponent.State = weaponObjectState;
        weaponObjectComponent.BulletsLeftInMagazine = weaponObjectState.BulletsLeftInMagazine;
        weaponObjectComponent.BulletsLeftOutOfMagazine = weaponObjectState.BulletsLeftOutOfMagazine;

        var rigidbody = weaponObjectComponent.Rigidbody;
        rigidbody.velocity = weaponObjectState.RigidBodyState.Velocity;
        rigidbody.angularVelocity = weaponObjectState.RigidBodyState.AngularVelocity;

        return weaponObject;
    }
    public GameObject SpawnLocalGrenadeObject(GrenadeState grenadeState)
    {
        var grenadePrefab = GetGrenadePrefab(grenadeState.Type);
        var grenadeObject = Instantiate(
            grenadePrefab,
            grenadeState.RigidBodyState.Position,
            Quaternion.Euler(grenadeState.RigidBodyState.EulerAngles)
        );

        var grenadeComponent = grenadeObject.GetComponent<GrenadeComponent>();
        grenadeComponent.State = grenadeState;

        var rigidbody = grenadeComponent.Rigidbody;
        rigidbody.velocity = grenadeState.RigidBodyState.Velocity;
        rigidbody.angularVelocity = grenadeState.RigidBodyState.AngularVelocity;

        return grenadeObject;
    }

    public GameObject FindPlayerObject(uint playerId)
    {
        var playerObjectComponent = FindPlayerObjectComponent(playerId);
        return playerObjectComponent?.gameObject;
    }
    public PlayerComponent FindPlayerComponent(uint playerId)
    {
        return FindObjectsOfType<PlayerComponent>()
            .FirstOrDefault(pc => pc.State.Id == playerId);
    }
    public PlayerObjectComponent FindPlayerObjectComponent(uint playerId)
    {
        return FindObjectsOfType<PlayerObjectComponent>()
            .FirstOrDefault(poc => poc.State.Id == playerId);
    }

    public WeaponComponent FindWeaponComponent(uint weaponId)
    {
        return FindObjectsOfType<WeaponComponent>()
            .FirstOrDefault(wc => wc.State?.Id == weaponId);
    }
    public GameObject FindWeaponObject(uint weaponId)
    {
        var weaponComponent = FindWeaponComponent(weaponId);
        return weaponComponent?.gameObject;
    }
    public GrenadeComponent FindGrenadeComponent(uint id)
    {
         return FindObjectsOfType<GrenadeComponent>()
            .FirstOrDefault(g => g.State.Id == id);
    }
    
    public WeaponSpawnerComponent FindWeaponSpawnerComponent(uint id)
    {
        return FindObjectsOfType<WeaponSpawnerComponent>()
            .FirstOrDefault(wsc => wsc.State.Id == id);
    }

    public PlayerInput GetCurrentPlayersInput()
    {
        return new PlayerInput
        {
            IsMoveFowardPressed = Input.GetKey(MoveForwardKeyCode),
            IsMoveBackwardPressed = Input.GetKey(MoveBackwardKeyCode),
            IsMoveRightPressed = Input.GetKey(MoveRightKeyCode),
            IsMoveLeftPressed = Input.GetKey(MoveLeftKeyCode),
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

    public void UpdatePlayerMovement(PlayerObjectState playerObjectState)
    {
        var playerObjectComponent = FindPlayerObjectComponent(playerObjectState.Id);
        if (playerObjectComponent == null) return;

        ApplyLookDirAnglesToPlayer(playerObjectComponent, playerObjectState.LookDirAngles);

        var relativeMoveDirection = GetRelativeMoveDirection(playerObjectState.Input);
        playerObjectComponent.Rigidbody.AddRelativeForce(1000 * relativeMoveDirection);
    }
    public void UpdatePlayer(PlayerObjectComponent playerObjectComponent)
    {
        var playerObjectState = playerObjectComponent.State;

        // reload
        if (playerObjectState.IsReloading)
        {
            playerObjectState.ReloadTimeLeft -= Time.deltaTime;
        }

        // shot interval
        if ((playerObjectState.CurrentWeapon != null) && (playerObjectState.CurrentWeapon.TimeUntilCanShoot > 0))
        {
            playerObjectState.CurrentWeapon.TimeUntilCanShoot -= Time.deltaTime;
        }

        // grenade throw interval
        if (playerObjectState.TimeUntilCanThrowGrenade > 0)
        {
            playerObjectState.TimeUntilCanThrowGrenade -= Time.deltaTime;
        }

        // update movement
        UpdatePlayerMovement(playerObjectState);
    }

    public Vector2 GetPlayerLookDirAngles(PlayerObjectComponent playerComponent)
    {
        return new Vector2(
            playerComponent.CameraPointObject.transform.localEulerAngles.x,
            playerComponent.transform.eulerAngles.y
        );
    }
    public void ApplyLookDirAnglesToPlayer(PlayerObjectComponent playerComponent, Vector2 LookDirAngles)
    {
        playerComponent.transform.localEulerAngles = new Vector3(0, LookDirAngles.y, 0);
        playerComponent.CameraPointObject.transform.localEulerAngles = new Vector3(LookDirAngles.x, 0, 0);
    }

    [Rpc(ExecuteOn = NetworkPeerType.Server)]
    public void ServerOnPlayerReloadPressed(uint playerId)
    {
        // TODO: Make sure the player ID is correct.
        var playerObjectComponent = OsFps.Instance.FindPlayerObjectComponent(playerId);

        if (playerObjectComponent.State.CanReload)
        {
            PlayerSystem.Instance.ServerPlayerStartReload(playerObjectComponent);
        }

        // TODO: Send to all other players???
    }

    [Rpc(ExecuteOn = NetworkPeerType.Server)]
    public void ServerOnPlayerTriggerPulled(uint playerId)
    {
        // TODO: Make sure the player ID is correct.
        var playerObjectComponent = OsFps.Instance.FindPlayerObjectComponent(playerId);
        PlayerSystem.Instance.ServerPlayerPullTrigger(Server, playerObjectComponent);

        var message = new TriggerPulledMessage
        {
            PlayerId = playerId
        };
        Server.SendMessageToAllClients(Server.reliableSequencedChannelId, message);
    }

    [Rpc(ExecuteOn = NetworkPeerType.Server)]
    public void ServerOnPlayerThrowGrenade(uint playerId)
    {
        // TODO: Make sure the player ID is correct.
        var playerObjectComponent = OsFps.Instance.FindPlayerObjectComponent(playerId);
        GrenadeSystem.Instance.ServerPlayerThrowGrenade(Server, playerObjectComponent);
    }

    [Rpc(ExecuteOn = NetworkPeerType.Server)]
    public void ServerOnChatMessage(uint playerId, string message)
    {
        var chatMessage = new ChatMessage
        {
            PlayerId = playerId,
            Message = message
        };

        Server.SendMessageToAllClients(Server.reliableSequencedChannelId, chatMessage);
    }

    [Rpc(ExecuteOn = NetworkPeerType.Server)]
    public void ServerOnChangeWeapon(uint playerId, byte weaponIndex)
    {
        var message = new ChangeWeaponMessage
        {
            PlayerId = playerId,
            WeaponIndex = weaponIndex
        };
        Server.SendMessageToAllClients(Server.reliableSequencedChannelId, message);

        var playerObjectComponent = OsFps.Instance.FindPlayerObjectComponent(playerId);

        if (playerObjectComponent == null) return;

        playerObjectComponent.State.CurrentWeaponIndex = weaponIndex;
        playerObjectComponent.State.ReloadTimeLeft = -1;
    }

    [Rpc(ExecuteOn = NetworkPeerType.Server)]
    public void ServerOnReceivePlayerInput(uint playerId, PlayerInput playerInput, Vector2 lookDirAngles)
    {
        // TODO: Make sure the player ID is correct.
        var playerObjectComponent = OsFps.Instance.FindPlayerObjectComponent(playerId);
        if (playerObjectComponent == null) return;

        var playerObjectState = playerObjectComponent.State;
        playerObjectState.Input = playerInput;
        playerObjectState.LookDirAngles = lookDirAngles;
    }

    // probably too much boilerplate here
    public void OnPlayerCollidingWithWeapon(GameObject playerObject, GameObject weaponObject)
    {
        if (Server != null)
        {
            PlayerSystem.Instance.ServerOnPlayerCollidingWithWeapon(Server, playerObject, weaponObject);
        }
    }

    public void OnPlayerCollidingWithGrenade(GameObject playerObject, GameObject grenadeObject)
    {
        if (Server != null)
        {
            PlayerSystem.Instance.ServerOnPlayerCollidingWithGrenade(playerObject, grenadeObject);
        }
    }

    public void GrenadeOnCollisionEnter(GrenadeComponent grenadeComponent, Collision collision)
    {
        if (Server != null)
        {
            GrenadeSystem.Instance.ServerGrenadeOnCollisionEnter(Server, grenadeComponent, collision);
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

        SetupRpcs();
    }
    private void Start()
    {
        // Initialize & configure network.
        NetworkTransport.Init();
    }
    private void OnDestroy()
    {
        // Don't do anything if we're destroying a duplicate OsFps object.
        if (this != Instance)
        {
            return;
        }

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

    public void CallRpcOnServer(string name, int channelId, object argumentsObj)
    {
        var rpcId = rpcIdByName[name];
        var rpcInfo = rpcInfoById[rpcId];

        Debug.Assert(rpcInfo.ExecuteOn == NetworkPeerType.Server);

        var messageBytes = NetworkSerializationUtils.SerializeRpcCall(rpcInfo, argumentsObj);
        Client.ClientPeer.SendMessageToServer(channelId, messageBytes);
    }
    public void CallRpcOnAllClients(string name, int channelId, object argumentsObj)
    {
        var rpcId = rpcIdByName[name];
        var rpcInfo = rpcInfoById[rpcId];

        Debug.Assert(rpcInfo.ExecuteOn == NetworkPeerType.Server);

        var messageBytes = NetworkSerializationUtils.SerializeRpcCall(rpcInfo, argumentsObj);
        Server.SendMessageToAllClients(channelId, messageBytes);
    }
    public void CallRpcOnClient(string name, int clientConnectionId, int channelId, object argumentsObj)
    {
        var rpcId = rpcIdByName[name];
        var rpcInfo = rpcInfoById[rpcId];

        Debug.Assert(rpcInfo.ExecuteOn == NetworkPeerType.Server);

        var messageBytes = NetworkSerializationUtils.SerializeRpcCall(rpcInfo, argumentsObj);
        Server.SendMessageToClient(clientConnectionId, channelId, messageBytes);
    }

    public void ExecuteRpc(byte id, params object[] arguments)
    {
        var rpcInfo = rpcInfoById[id];
        rpcInfo.MethodInfo.Invoke(this, arguments);
        Debug.Log("Executed an RPC!!!");
    }

    public Dictionary<string, byte> rpcIdByName;
    public Dictionary<byte, RpcInfo> rpcInfoById;
    private void SetupRpcs()
    {
        rpcIdByName = new Dictionary<string, byte>();
        rpcInfoById = new Dictionary<byte, RpcInfo>();

        var assembly = System.Reflection.Assembly.GetExecutingAssembly();

        foreach (var type in assembly.GetTypes())
        {
            foreach (var methodInfo in type.GetMethods())
            {
                var rpcAttribute = (RpcAttribute)methodInfo.GetCustomAttributes(typeof(RpcAttribute), inherit: false)
                    .FirstOrDefault();
                var parameterInfos = methodInfo.GetParameters();

                if (rpcAttribute != null)
                {
                    var rpcInfo = new RpcInfo
                    {
                        Id = (byte)(1 + rpcInfoById.Count),
                        Name = methodInfo.Name,
                        ExecuteOn = rpcAttribute.ExecuteOn,
                        MethodInfo = methodInfo,
                        ParameterNames = parameterInfos
                            .Select(parameterInfo => parameterInfo.Name)
                            .ToArray(),
                        ParameterTypes = parameterInfos
                            .Select(parameterInfo => parameterInfo.ParameterType)
                            .ToArray()
                    };

                    rpcIdByName.Add(rpcInfo.Name, rpcInfo.Id);
                    rpcInfoById.Add(rpcInfo.Id, rpcInfo);
                }
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