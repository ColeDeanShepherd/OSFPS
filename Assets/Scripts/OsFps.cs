﻿using System.Collections.Generic;
using System.Linq;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

public class OsFps : MonoBehaviour
{
    public const string LocalHostIpv4Address = "127.0.0.1";

    public const string StartSceneName = "Start";
    public const string SmallMapSceneName = "Small Map";

    public const string PlayerTag = "Player";
    public const string SpawnPointTag = "Respawn";
    public const string PlayerHeadColliderName = "Head";

    public const bool ShowHitScanShotsOnServer = true;
    public const bool ShowLagCompensationOnServer = false;
    public const float HitScanShotDebugLineLifetime = 1;

    public const float MinMouseSensitivity = 1;
    public const float MaxMouseSensitivity = 10;

    public const float MinFieldOfViewY = 60;
    public const float MaxFieldOfViewY = 110;

    public const float MaxPlayerMovementSpeed = 4;
    public const float PlayerInitialJumpSpeed = 5;
    public const float TimeAfterDamageUntilShieldRegen = 2;
    public const float ShieldRegenPerSecond = MaxPlayerShield / 2;
    public const float MaxPlayerShield = 70;
    public const float MaxPlayerHealth = 30;
    public const float RespawnTime = 3;
    public const float LagCompensationBufferTime = 1;

    public const float MaxWeaponPickUpDistance = 0.75f;

    public const int ShotgunBulletsPerShot = 15;
    public const float ShotgunShotConeAngleInDegrees = 15;

    public const float RocketSpeed = 15;
    public const float RocketExplosionRadius = 4;
    public const float RocketExplosionForce = 1000;
    public const float RocketExplosionDuration = 0.5f;
    public const float MaxRocketLifetime = 30;

    public const float MuzzleFlashDuration = 0.1f;
    public const int MaxWeaponCount = 2;
    public const int MaxGrenadeSlotCount = 2;

    public const int MaxGrenadesPerType = 2;
    public const float GrenadeThrowInterval = 1;
    public const float GrenadeThrowSpeed = 20;
    public const float GrenadeExplosionForce = 500;
    public const float GrenadeExplosionDuration = 0.5f;

    public const int FireMouseButtonNumber = 0;
    public const int ThrowGrenadeMouseButtonNumber = 1;
    public const KeyCode MoveForwardKeyCode = KeyCode.W;
    public const KeyCode MoveBackwardKeyCode = KeyCode.S;
    public const KeyCode MoveRightKeyCode = KeyCode.D;
    public const KeyCode MoveLeftKeyCode = KeyCode.A;
    public const KeyCode JumpKeyCode = KeyCode.Space;
    public const KeyCode ReloadKeyCode = KeyCode.R;
    public const KeyCode SwitchGrenadeTypeKeyCode = KeyCode.G;
    public const KeyCode PickupWeaponKeyCode = KeyCode.E;
    public const KeyCode ZoomInKeyCode = KeyCode.Z;
    public const KeyCode ShowScoreboardKeyCode = KeyCode.Tab;
    public const KeyCode ChatKeyCode = KeyCode.Return;
    public const KeyCode ToggleMenuKeyCode = KeyCode.Escape;

    public const float KillPlaneY = -100;

    public const string ShieldDownMaterialAlphaParameterName = "Vector1_14FF3C92";

    public static OsFps Instance;
    public static CustomLogger Logger = new CustomLogger(Debug.unityLogger.logHandler);
    
    public Server Server;
    public Client Client;
    public Stack<MonoBehaviour> MenuStack;
    public bool IsInOptionsScreen
    {
        get
        {
            return MenuStack.Any() && (MenuStack.Peek() is OptionsScreenComponent);
        }
    }
    public Settings Settings;

    public bool IsServer
    {
        get
        {
            return Server != null;
        }
    }
    public bool IsRemoteClient
    {
        get
        {
            return (Server == null) && (Client != null);
        }
    }

    [HideInInspector]
    public GameObject CanvasObject;

    [HideInInspector]
    public List<WeaponDefinitionComponent> WeaponDefinitionComponents;
    [HideInInspector]
    public List<GrenadeDefinitionComponent> GrenadeDefinitionComponents;

    #region Inspector-set Variables
    public GameObject MainMenuPrefab;
    public GameObject OptionsScreenPrefab;
    public GameObject PauseScreenPrefab;
    public GameObject ConnectingScreenPrefab;

    public GameObject PlayerPrefab;
    public GameObject CameraPrefab;

    public GameObject[] WeaponDefinitionPrefabs;
    public GameObject[] GrenadeDefinitionPrefabs;

    public AudioClip ReloadSound;

    public GameObject RocketPrefab;
    public GameObject RocketExplosionPrefab;
    public AudioClip RocketExplosionSound;

    public AudioClip FragGrenadeBounceSound;

    public Material SniperBulletTrailMaterial;

    public GameObject MuzzleFlashPrefab;

    public GameObject GUIContainerPrefab;
    public GameObject CrosshairPrefab;

    public Material ClientShotRayMaterial;
    public Material ServerShotRayMaterial;
    public Material ShieldDownMaterial;
    #endregion

    public ConnectionConfig CreateConnectionConfig(
        out int reliableSequencedChannelId,
        out int reliableChannelId,
        out int unreliableStateUpdateChannelId,
        out int unreliableFragmentedChannelId,
        out int unreliableChannelId
    )
    {
        var connectionConfig = new ConnectionConfig();
        reliableSequencedChannelId = connectionConfig.AddChannel(QosType.ReliableSequenced);
        reliableChannelId = connectionConfig.AddChannel(QosType.Reliable);
        unreliableStateUpdateChannelId = connectionConfig.AddChannel(QosType.StateUpdate);
        unreliableFragmentedChannelId = connectionConfig.AddChannel(QosType.UnreliableFragmented);
        unreliableChannelId = connectionConfig.AddChannel(QosType.Unreliable);

        return connectionConfig;
    }
    
    public GameObject CreateLocalPlayerDataObject(PlayerState playerState)
    {
        var playerDataObject = new GameObject($"Player {playerState.Id}");

        var playerComponent = playerDataObject.AddComponent<PlayerComponent>();
        playerComponent.State = playerState;

        playerDataObject.AddComponent<GameObjectEntity>();

        return playerDataObject;
    }

    public void SetShieldAlpha(PlayerObjectComponent playerObjectComponent, float alpha)
    {
        foreach (var meshRenderer in playerObjectComponent.GetComponentsInChildren<MeshRenderer>())
        {
            var shieldDownMaterial = meshRenderer.materials
                .FirstOrDefault(m => m.name.Contains(OsFps.Instance.ShieldDownMaterial.name));
            if (shieldDownMaterial != null)
            {
                shieldDownMaterial.SetFloat(ShieldDownMaterialAlphaParameterName, alpha);
            }
        }
    }
    public GameObject SpawnLocalPlayer(PlayerObjectState playerObjectState)
    {
        var playerObject = Instantiate(
            PlayerPrefab, playerObjectState.Position, Quaternion.Euler(playerObjectState.LookDirAngles)
        );

        var playerObjectComponent = playerObject.GetComponent<PlayerObjectComponent>();
        playerObjectComponent.State = playerObjectState;
        playerObjectComponent.Rigidbody.velocity = playerObjectState.Velocity;

        SetShieldAlpha(playerObjectComponent, 0);

        return playerObject;
    }
    public GameObject SpawnLocalWeaponObject(WeaponObjectState weaponObjectState)
    {
        var weaponPrefab = GetWeaponDefinitionByType(weaponObjectState.Type).Prefab;
        var weaponObject = Instantiate(
            weaponPrefab,
            weaponObjectState.RigidBodyState.Position,
            Quaternion.Euler(weaponObjectState.RigidBodyState.EulerAngles)
        );

        var weaponObjectComponent = weaponObject.GetComponent<WeaponComponent>();
        weaponObjectComponent.State = weaponObjectState;

        var rigidbody = weaponObjectComponent.Rigidbody;
        rigidbody.velocity = weaponObjectState.RigidBodyState.Velocity;
        rigidbody.angularVelocity = weaponObjectState.RigidBodyState.AngularVelocity;

        return weaponObject;
    }
    public GameObject SpawnLocalGrenadeObject(GrenadeState grenadeState)
    {
        var grenadePrefab = GetGrenadeDefinitionByType(grenadeState.Type).Prefab;
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
    public GameObject SpawnLocalRocketObject(RocketState rocketState)
    {
        var rocketObject = Instantiate(
            RocketPrefab,
            rocketState.RigidBodyState.Position,
            Quaternion.Euler(rocketState.RigidBodyState.EulerAngles)
        );

        var rocketComponent = rocketObject.GetComponent<RocketComponent>();
        rocketComponent.State = rocketState;

        var rigidbody = rocketComponent.Rigidbody;
        rigidbody.velocity = rocketState.RigidBodyState.Velocity;
        rigidbody.angularVelocity = rocketState.RigidBodyState.AngularVelocity;

        return rocketObject;
    }

    public WeaponDefinition GetWeaponDefinitionByType(WeaponType type)
    {
        return Instance.WeaponDefinitionComponents
            .FirstOrDefault(wdc => wdc.Definition.Type == type)
            ?.Definition;
    }
    public GrenadeDefinition GetGrenadeDefinitionByType(GrenadeType type)
    {
        return Instance.GrenadeDefinitionComponents
            .FirstOrDefault(gdc => gdc.Definition.Type == type)
            ?.Definition;
    }

    public void ApplyExplosionDamageAndForces(
        Server server, Vector3 explosionPosition, float explosionRadius, float maxExplosionForce,
        float maxDamage, uint? attackerPlayerId
    )
    {
        // apply damage & forces to players within range
        var affectedColliders = Physics.OverlapSphere(explosionPosition, explosionRadius);
        var affectedColliderPlayerObjectComponents = affectedColliders
            .Select(collider => collider.gameObject.FindComponentInObjectOrAncestor<PlayerObjectComponent>())
            .ToArray();

        var affectedPlayerPointPairs = affectedColliders
            .Select((collider, colliderIndex) =>
                new System.Tuple<PlayerObjectComponent, Vector3>(
                    affectedColliderPlayerObjectComponents[colliderIndex],
                    collider.ClosestPoint(explosionPosition)
                )
            )
            .Where(pair => pair.Item1 != null)
            .GroupBy(pair => pair.Item1)
            .Select(g => g
                .OrderBy(pair => Vector3.Distance(pair.Item2, explosionPosition))
                .FirstOrDefault()
            )
            .ToArray();

        foreach (var pair in affectedPlayerPointPairs)
        {
            // Apply damage.
            var playerObjectComponent = pair.Item1;
            var closestPointToExplosion = pair.Item2;

            var distanceFromExplosion = Vector3.Distance(closestPointToExplosion, explosionPosition);
            var unclampedDamagePercent = (explosionRadius - distanceFromExplosion) / explosionRadius;
            var damagePercent = Mathf.Max(unclampedDamagePercent, 0);
            var damage = damagePercent * maxDamage;

            // TODO: don't call system directly
            var attackingPlayerObjectComponent = attackerPlayerId.HasValue
                ? OsFps.Instance.FindPlayerObjectComponent(attackerPlayerId.Value)
                : null;
            PlayerSystem.Instance.ServerDamagePlayer(
                server, playerObjectComponent, damage, attackingPlayerObjectComponent
            );

            // Apply forces.
            var rigidbody = playerObjectComponent.gameObject.GetComponent<Rigidbody>();
            if (rigidbody != null)
            {
                rigidbody.AddExplosionForce(maxExplosionForce, explosionPosition, explosionRadius);
            }
        }

        for (var colliderIndex = 0; colliderIndex < affectedColliders.Length; colliderIndex++)
        {
            if (affectedColliderPlayerObjectComponents[colliderIndex] != null) continue;

            var collider = affectedColliders[colliderIndex];

            // Apply forces.
            var rigidbody = collider.gameObject.GetComponent<Rigidbody>();
            if (rigidbody != null)
            {
                rigidbody.AddExplosionForce(maxExplosionForce, explosionPosition, explosionRadius);
            }
        }
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
    public RocketComponent FindRocketComponent(uint id)
    {
        return FindObjectsOfType<RocketComponent>()
           .FirstOrDefault(g => g.State.Id == id);
    }

    public WeaponSpawnerComponent FindWeaponSpawnerComponent(uint id)
    {
        return FindObjectsOfType<WeaponSpawnerComponent>()
            .FirstOrDefault(wsc => wsc.State.Id == id);
    }
    public GrenadeSpawnerComponent FindGrenadeSpawnerComponent(uint id)
    {
        return FindObjectsOfType<GrenadeSpawnerComponent>()
            .FirstOrDefault(gsc => gsc.State.Id == id);
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
    
    public bool IsPlayerGrounded(PlayerObjectComponent playerObjectComponent)
    {
        var sphereRadius = 0.4f;
        var spherePosition = playerObjectComponent.transform.position + new Vector3(0, 0.3f, 0);

        var intersectingColliders = Physics.OverlapSphere(spherePosition, sphereRadius);
        return intersectingColliders.Any(collider =>
        {
            var otherPlayerObjectComponent = collider.gameObject.FindComponentInObjectOrAncestor<PlayerObjectComponent>();
            return (
                (otherPlayerObjectComponent == null) ||
                (otherPlayerObjectComponent.State.Id != playerObjectComponent.State.Id)
            );
        });
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
            playerObjectState.CurrentWeapon.TimeSinceLastShot += Time.deltaTime;
        }

        // grenade throw interval
        if (playerObjectState.TimeUntilCanThrowGrenade > 0)
        {
            playerObjectState.TimeUntilCanThrowGrenade -= Time.deltaTime;
        }

        // shield regen interval
        float shieldRegenTime;
        if (playerObjectState.TimeUntilShieldCanRegen > 0)
        {
            playerObjectState.TimeUntilShieldCanRegen -= Time.deltaTime;
            shieldRegenTime = (playerObjectState.TimeUntilShieldCanRegen <= 0)
                ? Mathf.Abs(playerObjectState.TimeUntilShieldCanRegen)
                : 0;
        }
        else
        {
            shieldRegenTime = Time.deltaTime;
        }

        var shieldRegenAmount = shieldRegenTime * OsFps.ShieldRegenPerSecond;
        playerObjectState.Shield = Mathf.Min(playerObjectState.Shield + shieldRegenAmount, OsFps.MaxPlayerShield);

        // update movement
        PlayerSystem.Instance.UpdatePlayerMovement(playerObjectState);
    }

    public Vector2 GetPlayerLookDirAngles(PlayerObjectComponent playerObjectComponent)
    {
        return new Vector2(
            playerObjectComponent.CameraPointObject.transform.localEulerAngles.x,
            playerObjectComponent.transform.eulerAngles.y
        );
    }
    public void ApplyLookDirAnglesToPlayer(PlayerObjectComponent playerObjectComponent, Vector2 LookDirAngles)
    {
        playerObjectComponent.transform.localEulerAngles = new Vector3(0, LookDirAngles.y, 0);
        playerObjectComponent.CameraPointObject.transform.localEulerAngles = new Vector3(LookDirAngles.x, 0, 0);
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
    public void RocketOnCollisionEnter(RocketComponent rocketComponent, Collision collision)
    {
        if (Server != null)
        {
            RocketSystem.Instance.ServerRocketOnCollisionEnter(Server, rocketComponent, collision);
        }
    }

    public GameObject CreateSniperBulletTrail(Ray ray)
    {
        var sniperBulletTrail = new GameObject("sniperBulletTrail");
        var lineRenderer = sniperBulletTrail.AddComponent<LineRenderer>();
        lineRenderer.SetPositions(new []
        {
            ray.origin,
            ray.origin + (2000 * ray.direction)
        });
        lineRenderer.material = SniperBulletTrailMaterial;

        var lineWidth = 0.1f;
        lineRenderer.startWidth = lineWidth;
        lineRenderer.endWidth = lineWidth;

        Object.Destroy(sniperBulletTrail, 1);

        return sniperBulletTrail;
    }

    public MainMenuComponent CreateMainMenu()
    {
        var mainMenuObject = Instantiate(MainMenuPrefab, CanvasObject.transform);
        return mainMenuObject.GetComponent<MainMenuComponent>();
    }
    public OptionsScreenComponent CreateOptionsScreen()
    {
        var optionsScreenObject = Instantiate(OptionsScreenPrefab, CanvasObject.transform);
        return optionsScreenObject.GetComponent<OptionsScreenComponent>();
    }

    private void Awake()
    {
        Assert.raiseExceptions = true;

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

        CreateDataObject();
        CanvasObject = guiContainer.FindDescendant("Canvas");

        SetupRpcs();

        LoadSettings();
    }
    private GameObject CreateDataObject()
    {
        var dataObject = new GameObject("data");
        DontDestroyOnLoad(dataObject);

        foreach (var weaponDefinitionPrefab in WeaponDefinitionPrefabs)
        {
            GameObject weaponDefinitionObject = Instantiate(weaponDefinitionPrefab, dataObject.transform);
            WeaponDefinitionComponents.Add(weaponDefinitionObject.GetComponent<WeaponDefinitionComponent>());
        }
        foreach (var grenadeDefinitionPrefab in GrenadeDefinitionPrefabs)
        {
            GameObject grenadeDefinitionObject = Instantiate(grenadeDefinitionPrefab, dataObject.transform);
            GrenadeDefinitionComponents.Add(grenadeDefinitionObject.GetComponent<GrenadeDefinitionComponent>());
        }

        return dataObject;
    }
    private void Start()
    {
        // Initialize & configure network.
        NetworkTransport.Init();

        MenuStack = new Stack<MonoBehaviour>();
        PushMenu(CreateMainMenu());
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
        if(!((Server == null) && (Client == null)))
        {
            if (Client != null)
            {
                Client.OnGui();
            }
        }
    }

    public void PushMenu(MonoBehaviour menuComponent)
    {
        if (MenuStack.Any())
        {
            MenuStack.Peek().gameObject.SetActive(false);
        }

        MenuStack.Push(menuComponent);
    }
    public void PopMenu()
    {
        if (MenuStack.Any())
        {
            Destroy(MenuStack.Peek().gameObject);
            MenuStack.Pop();
        }
        
        if (MenuStack.Any())
        {
            MenuStack.Peek().gameObject.SetActive(true);
        }
    }

    public void StopServer()
    {
        if (Server == null) return;

        Server.Stop();
        Server = null;
    }

    public string SettingsFilePath
    {
        get
        {
            return Application.persistentDataPath + "/settings.json";
        }
    }

    private void LoadSettings()
    {
        if (System.IO.File.Exists(SettingsFilePath))
        {
            var settingsJsonString = System.IO.File.ReadAllText(SettingsFilePath, System.Text.Encoding.UTF8);
            Settings = JsonUtility.FromJson<Settings>(settingsJsonString);
        }
        else
        {
            Settings = new Settings();
        }
    }
    public void SaveSettings()
    {
        System.IO.File.WriteAllText(SettingsFilePath, JsonUtility.ToJson(Settings));
    }
    
    public void CallRpcOnServer(string name, int channelId, object argumentsObj)
    {
        var rpcId = rpcIdByName[name];
        var rpcInfo = rpcInfoById[rpcId];

        Assert.IsTrue(rpcInfo.ExecuteOn == NetworkPeerType.Server);

        var messageBytes = NetworkSerializationUtils.SerializeRpcCall(rpcInfo, argumentsObj);
        Client.ClientPeer.SendMessageToServer(channelId, messageBytes);
    }
    public void CallRpcOnAllClients(string name, int channelId, object argumentsObj)
    {
        var rpcId = rpcIdByName[name];
        var rpcInfo = rpcInfoById[rpcId];

        Assert.IsTrue(rpcInfo.ExecuteOn == NetworkPeerType.Client);

        var messageBytes = NetworkSerializationUtils.SerializeRpcCall(rpcInfo, argumentsObj);
        Server.SendMessageToAllClients(channelId, messageBytes);
    }
    public void CallRpcOnAllClientsExcept(string name, int exceptClientConnectionId, int channelId, object argumentsObj)
    {
        var rpcId = rpcIdByName[name];
        var rpcInfo = rpcInfoById[rpcId];

        Assert.IsTrue(rpcInfo.ExecuteOn == NetworkPeerType.Client);

        var messageBytes = NetworkSerializationUtils.SerializeRpcCall(rpcInfo, argumentsObj);
        Server.SendMessageToAllClientsExcept(exceptClientConnectionId, channelId, messageBytes);
    }
    public void CallRpcOnClient(string name, int clientConnectionId, int channelId, object argumentsObj)
    {
        var rpcId = rpcIdByName[name];
        var rpcInfo = rpcInfoById[rpcId];

        Assert.IsTrue(rpcInfo.ExecuteOn == NetworkPeerType.Client);

        var messageBytes = NetworkSerializationUtils.SerializeRpcCall(rpcInfo, argumentsObj);
        Server.SendMessageToClient(clientConnectionId, channelId, messageBytes);
    }

    public void ExecuteRpc(byte id, params object[] arguments)
    {
        var rpcInfo = rpcInfoById[id];
        var objContainingRpc = (rpcInfo.ExecuteOn == NetworkPeerType.Server)
            ? (object)Server
            : (object)Client;
        rpcInfo.MethodInfo.Invoke(objContainingRpc, arguments);

        OsFps.Logger.Log($"Executed RPC {rpcInfo.Name}");
    }

    public void CreateHitScanShotDebugLine(Ray ray, Material material)
    {
        var hitScanShotObject = new GameObject("Hit Scan Shot");

        var lineRenderer = hitScanShotObject.AddComponent<LineRenderer>();
        var lineWidth = 0.05f;
        lineRenderer.startWidth = lineWidth;
        lineRenderer.endWidth = lineWidth;
        lineRenderer.SetPositions(new Vector3[] {
            ray.origin,
            ray.origin + (1000 * ray.direction)
        });
        lineRenderer.sharedMaterial = material;

        Object.Destroy(hitScanShotObject, OsFps.HitScanShotDebugLineLifetime);
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
            var methodBindingFlags =
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance;

            foreach (var methodInfo in type.GetMethods(methodBindingFlags))
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

    private bool ParseIpAddressAndPort(
        string ipAddressAndPort, ushort defaultPortNumber, out string ipAddress, out ushort portNumber
    )
    {
        var splitIpAddressAndPort = ipAddressAndPort.Split(new[] { ':' });

        if (splitIpAddressAndPort.Length == 1)
        {
            ipAddress = splitIpAddressAndPort[0];
            portNumber = defaultPortNumber;
            return true;

        }
        else if (splitIpAddressAndPort.Length == 2)
        {
            ipAddress = splitIpAddressAndPort[0];
            return ushort.TryParse(splitIpAddressAndPort[1], out portNumber);
        }
        else
        {
            ipAddress = "";
            portNumber = 0;
            return false;
        }
    }

    public string EnteredClientIpAddressAndPort;
    public void OnMapLoadedAsClient(Scene scene, LoadSceneMode loadSceneMode)
    {
        SceneManager.sceneLoaded -= OnMapLoadedAsClient;

        Client = new Client();
        Client.OnDisconnectedFromServer += OnClientDisconnectedFromServer;
        Client.Start(true);

        string ipAddress;
        ushort portNumber;
        var succeededParsing = ParseIpAddressAndPort(
            EnteredClientIpAddressAndPort, Server.PortNumber, out ipAddress, out portNumber
        );

        if (succeededParsing)
        {
            Client.StartConnectingToServer(ipAddress, portNumber);
        }
    }
    private void OnClientDisconnectedFromServer()
    {
        ShutdownNetworkPeers();

        SceneManager.LoadScene(StartSceneName);
        PushMenu(CreateMainMenu());
    }
}