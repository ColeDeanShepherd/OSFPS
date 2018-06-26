using System.Collections.Generic;
using System.Linq;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using NetworkLibrary;
using UnityEngine.Profiling;

public class OsFps : MonoBehaviour
{
    #region Constants
    public const string LocalHostIpv4Address = "127.0.0.1";
    public const byte StateSynchronizationMessageId = 0;

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
    public const float SmgShotConeAngleInDegrees = 5;
    public const float AssaultRifleShotConeAngleInDegrees = 2.5f;

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

    public const float KillPlaneY = -100;

    public const string ShieldDownMaterialAlphaParameterName = "Vector1_14FF3C92";
    #endregion

    public static OsFps Instance;
    public static CustomLogger Logger = new CustomLogger(Debug.unityLogger.logHandler);

    public static RigidBodyState ToRigidBodyState(Rigidbody rigidbody)
    {
        return new RigidBodyState
        {
            Position = rigidbody.transform.position,
            EulerAngles = rigidbody.transform.eulerAngles,
            Velocity = rigidbody.velocity,
            AngularVelocity = rigidbody.angularVelocity
        };
    }
    public static void ApplyRigidbodyState(RigidBodyState newRigidBodyState, RigidBodyState oldRigidBodyState, Rigidbody rigidbody, float roundTripTime)
    {
        // Correct position.
        var correctedPosition = CorrectedPosition(
            newRigidBodyState.Position, newRigidBodyState.Velocity,
            roundTripTime, rigidbody.transform.position
        );
        rigidbody.transform.position = correctedPosition;
        newRigidBodyState.Position = correctedPosition;

        // Correct orientation.
        var correctedEulerAngles = CorrectedEulerAngles(
            newRigidBodyState.EulerAngles, newRigidBodyState.AngularVelocity,
            roundTripTime, rigidbody.transform.eulerAngles
        );
        rigidbody.transform.eulerAngles = correctedEulerAngles;
        newRigidBodyState.EulerAngles = correctedEulerAngles;

        // Correct velocity.
        var correctedVelocity = CorrectedVelocity(
            newRigidBodyState.Velocity, roundTripTime, rigidbody.velocity
        );
        rigidbody.velocity = correctedVelocity;
        newRigidBodyState.Velocity = correctedVelocity;

        // Correct angular velocity.
        var correctedAngularVelocity = CorrectedAngularVelocity(
            newRigidBodyState.AngularVelocity, roundTripTime, rigidbody.angularVelocity
        );
        rigidbody.angularVelocity = correctedAngularVelocity;
        newRigidBodyState.AngularVelocity = correctedAngularVelocity;
    }
    public static Vector3 CorrectedPosition(Vector3 serverPosition, Vector3 serverVelocity, float roundTripTime, Vector3 clientPosition)
    {
        var serverToClientLatency = roundTripTime / 2;
        var predictedPosition = serverPosition + (serverToClientLatency * serverVelocity);
        var positionDifference = predictedPosition - clientPosition;
        var percentOfDiffToCorrect = 1f / 3;
        var positionDelta = percentOfDiffToCorrect * positionDifference;
        return clientPosition + positionDelta;
    }
    public static Vector3 CorrectedEulerAngles(Vector3 serverEulerAngles, Vector3 serverAngularVelocity, float roundTripTime, Vector3 clientEulerAngles)
    {
        const bool correctSmoothly = false;

        if (!correctSmoothly)
        {
            return serverEulerAngles;
        }
        else
        {
            var serverToClientLatency = roundTripTime / 2;
            var predictedEulerAngles = serverEulerAngles + (serverToClientLatency * serverAngularVelocity);
            var eulerAnglesDifference = predictedEulerAngles - clientEulerAngles;
            var percentOfDiffToCorrect = 1f / 3;
            var eulerAnglesDelta = percentOfDiffToCorrect * eulerAnglesDifference;
            return clientEulerAngles + eulerAnglesDelta;
        }
    }
    public static Vector3 CorrectedVelocity(Vector3 serverVelocity, float roundTripTime, Vector3 clientVelocity)
    {
        var serverToClientLatency = roundTripTime / 2;
        var percentOfDiffToCorrect = 1f / 2;
        var velocityDiff = percentOfDiffToCorrect * (serverVelocity - clientVelocity);
        return clientVelocity + velocityDiff;
    }
    public static Vector3 CorrectedAngularVelocity(Vector3 serverAngularVelocity, float roundTripTime, Vector3 clientAngularVelocity)
    {
        var serverToClientLatency = roundTripTime / 2;
        var percentOfDiffToCorrect = 1f / 2;
        var angularVelocityDiff = percentOfDiffToCorrect * (serverAngularVelocity - clientAngularVelocity);
        return clientAngularVelocity + angularVelocityDiff;
    }

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
    public GameObject ChatBoxPrefab;
    public GameObject HealthBarPrefab;

    public GameObject PlayerPrefab;
    public GameObject CameraPrefab;

    public GameObject[] WeaponDefinitionPrefabs;
    public GameObject[] GrenadeDefinitionPrefabs;

    public GameObject WeaponSpawnerPrefab;
    public GameObject GrenadeSpawnerPrefab;

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

    public GameObject BulletHolePrefab;

    public RuntimeAnimatorController RecoilAnimatorController;
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
                ? PlayerSystem.Instance.FindPlayerObjectComponent(attackerPlayerId.Value)
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

    public static byte PlayerInputAsBits(PlayerInput playerInput)
    {
        byte bits = 0;
        BitUtilities.SetBit(ref bits, 0, playerInput.IsMoveFowardPressed);
        BitUtilities.SetBit(ref bits, 1, playerInput.IsMoveBackwardPressed);
        BitUtilities.SetBit(ref bits, 2, playerInput.IsMoveRightPressed);
        BitUtilities.SetBit(ref bits, 3, playerInput.IsMoveLeftPressed);
        BitUtilities.SetBit(ref bits, 4, playerInput.IsFirePressed);

        return bits;
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
        
        NetLib.Setup();

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
        AudioListener.volume = Settings.Volume;

        if (Server != null)
        {
            Profiler.BeginSample("Server Update");
            Server.Update();
            Profiler.EndSample();
        }

        if (Client != null)
        {
            Profiler.BeginSample("Client Update");
            Client.Update();
            Profiler.EndSample();
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
            if (Server != null)
            {
                Server.OnGui();
            }

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

    public string EnteredClientIpAddressAndPort;
    public void OnMapLoadedAsClient(Scene scene, LoadSceneMode loadSceneMode)
    {
        SceneManager.sceneLoaded -= OnMapLoadedAsClient;

        Client = new Client();
        Client.OnDisconnectedFromServer += OnClientDisconnectedFromServer;
        Client.Start(true);

        string ipAddress;
        ushort portNumber;
        var succeededParsing = NetLib.ParseIpAddressAndPort(
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