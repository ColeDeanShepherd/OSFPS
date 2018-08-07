using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using NetworkLibrary;
using UnityEngine.Profiling;
using UnityEngine.Networking.Match;

public class OsFps : MonoBehaviour
{
    #region Constants
    public const string StartSceneName = "Start";
    public const string SmallMapSceneName = "Small Map";

    public const string PlayerTag = "Player";
    public const string SpawnPointTag = "Respawn";
    public const string PlayerHeadColliderName = "Head";

    public const string ShieldDownMaterialAlphaParameterName = "Vector1_14FF3C92";

    public const int MatchmakingRequestDomain = 0;

    public static bool ShowHitScanShotsOnServer = true;
    public static bool ShowLagCompensationOnServer = false;
    public static float HitScanShotDebugLineLifetime = 1;

    public const float MinMouseSensitivity = 1;
    public const float MaxMouseSensitivity = 10;

    public const float MinFieldOfViewY = 60;
    public const float MaxFieldOfViewY = 110;

    public const float MaxPlayerMovementSpeed = 5;
    public const float PlayerInitialJumpSpeed = 5;
    public const float TimeAfterDamageUntilShieldRegen = 2;
    public const float ShieldRegenPerSecond = MaxPlayerShield / 2;
    public const float MaxPlayerShield = 70;
    public const float MaxPlayerHealth = 30;
    public const float RespawnTime = 3;
    public const float LagCompensationBufferTime = 1;

    public const float MaxWeaponPickUpDistance = 0.75f;

    public const float EquipWeaponTime = 0.5f;

    public const int ShotgunBulletsPerShot = 15;

    public const float RocketSpeed = 20;
    public const float RocketExplosionRadius = 4;
    public const float RocketExplosionForce = 1000;
    public const float RocketExplosionDuration = 2;
    public const float MaxRocketLifetime = 30;

    public const float MuzzleFlashDuration = 0.1f;
    public const int MaxWeaponCount = 2;
    public const int MaxGrenadeSlotCount = 2;

    public const int MaxGrenadesPerType = 2;
    public const float GrenadeThrowInterval = 1;
    public const float GrenadeThrowSpeed = 20;
    public const float GrenadeExplosionForce = 500;
    public const float GrenadeExplosionDuration = 0.5f;

    public const float SniperRifleBulletTrailLifeTime = 3;

    public const float KillPlaneY = -100;
    #endregion

    public static OsFps Instance;
    public static CustomLogger Logger = new CustomLogger(Debug.unityLogger.logHandler);

    public static string SettingsFilePath
    {
        get
        {
            return Application.persistentDataPath + "/settings.json";
        }
    }

    public MatchInfo MatchInfo;
    public Server Server;
    public Client Client;
    public MenuStack MenuStack;
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
    public bool IsClient
    {
        get
        {
            return Client != null;
        }
    }
    public bool IsRemoteClient
    {
        get
        {
            return !IsServer && IsClient;
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
    public GameObject DedicatedServerScreenPrefab;
    public GameObject MatchmakingScreenPrefab;
    public GameObject ServerRowPrefab;
    public GameObject ChatBoxPrefab;
    public GameObject HealthBarPrefab;
    public GameObject TextPrefab;

    public GameObject PlayerPrefab;
    public GameObject CameraPrefab;

    public GameObject[] WeaponDefinitionPrefabs;
    public GameObject[] GrenadeDefinitionPrefabs;

    public GameObject WeaponSpawnerPrefab;
    public GameObject GrenadeSpawnerPrefab;

    public AudioClip ReloadSound;

    public GameObject RocketPrefab;
    public GameObject RocketExplosionPrefab;

    public AudioClip GunDryFireSound;
    public AudioClip SniperZoomSound;

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
    
    public MainMenuComponent EnterMainMenu()
    {
        var mainMenuObject = Instantiate(MainMenuPrefab, CanvasObject.transform);
        var mainMenuComponent = mainMenuObject.GetComponent<MainMenuComponent>();

        MenuStack.Push(mainMenuComponent);

        return mainMenuComponent;
    }
    public OptionsScreenComponent EnterOptionsScreen()
    {
        var optionsScreenObject = Instantiate(OptionsScreenPrefab, CanvasObject.transform);
        var optionsScreenComponent = optionsScreenObject.GetComponent<OptionsScreenComponent>();

        MenuStack.Push(optionsScreenComponent);

        return optionsScreenComponent;
    }
    public MatchmakingScreenComponent EnterMatchmakingScreen()
    {
        var matchmakingScreenObject = Instantiate(MatchmakingScreenPrefab, CanvasObject.transform);
        var matchmakingScreenComponent = matchmakingScreenObject.GetComponent<MatchmakingScreenComponent>();

        MenuStack.Push(matchmakingScreenComponent);

        return matchmakingScreenComponent;
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

        Assert.raiseExceptions = true;

        Instance = this;
        DontDestroyOnLoad(gameObject);

        GameObject guiContainer = Instantiate(GUIContainerPrefab);
        DontDestroyOnLoad(guiContainer);

        CreateDataObject();
        CanvasObject = guiContainer.FindDescendant("Canvas");

        var networkMatch = gameObject.AddComponent<NetworkMatch>();

        NetLib.NetworkMatch = networkMatch;
        NetLib.Setup();
        
        Settings.LoadFromFile(SettingsFilePath);
    }
    private void Start()
    {
        // Initialize & configure network.
        NetworkTransport.Init();

        MenuStack = new MenuStack();
        EnterMainMenu();
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
        if (PlayerObjectSystem.Instance != null)
        {
            PlayerObjectSystem.Instance.OnLateUpdate();
        }

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

    public void StartDedicatedServer()
    {
        MenuStack.Clear();
        Server = new Server();
        Server.Start();
    }
    public void StartListenServer()
    {
        EnteredClientIpAddressAndPort = NetLib.LocalHostIpv4Address + ":" + Server.PortNumber;
        
        MenuStack.Clear();

        SceneManager.sceneLoaded += OnMapLoadedAsClient;

        Server = new Server();
        Server.Start();
    }
    public void ShutdownServer()
    {
        ShutdownNetworkPeers();

        SceneManager.LoadScene(StartSceneName);
        EnterMainMenu();
    }

    public void ConnectToServerThroughMasterServer(MatchInfoSnapshot matchInfoSnapshot)
    {
        this.matchInfoSnapshot = matchInfoSnapshot;
        MenuStack.Clear();
        SceneManager.sceneLoaded += OnMapLoadedAsClientUsingMasterServer;
        SceneManager.LoadScene(SmallMapSceneName);
    }
    public void ConnectToServer(string ipAddressAndPort)
    {
        EnteredClientIpAddressAndPort = ipAddressAndPort;
        MenuStack.Clear();
        SceneManager.sceneLoaded += OnMapLoadedAsClient;
        SceneManager.LoadScene(SmallMapSceneName);
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
    public MatchInfoSnapshot matchInfoSnapshot;

    public void OnMapLoadedAsClient(Scene scene, LoadSceneMode loadSceneMode)
    {
        SceneManager.sceneLoaded -= OnMapLoadedAsClient;

        Client = new Client();
        Client.OnDisconnectedFromServer += OnClientDisconnectedFromServer;
        Client.Start();

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
    public void OnMapLoadedAsClientUsingMasterServer(Scene scene, LoadSceneMode loadSceneMode)
    {
        SceneManager.sceneLoaded -= OnMapLoadedAsClient;

        Client = new Client();
        Client.OnDisconnectedFromServer += OnClientDisconnectedFromServer;
        Client.Start();

        string ipAddress;
        ushort portNumber;
        var succeededParsing = NetLib.ParseIpAddressAndPort(
            EnteredClientIpAddressAndPort, Server.PortNumber, out ipAddress, out portNumber
        );

        if (succeededParsing)
        {
            Client.ClientPeer.StartConnectingToServerThroughMasterServer(
                matchInfoSnapshot, "", MatchmakingRequestDomain
            );
        }
    }
    private void OnClientDisconnectedFromServer()
    {
        ShutdownNetworkPeers();

        SceneManager.LoadScene(StartSceneName);
        EnterMainMenu();
    }
}