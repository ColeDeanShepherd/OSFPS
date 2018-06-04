using System.Collections.Generic;
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
    public const float PlayerInitialJumpSpeed = 4;
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

    public static WeaponDefinition PistolDefinition = new WeaponDefinition
    {
        Type = WeaponType.Pistol,
        MaxAmmo = 100,
        BulletsPerMagazine = 10,
        DamagePerBullet = 10,
        HeadShotDamagePerBullet = 25,
        ReloadTime = 1,
        ShotInterval = 0.4f,
        IsAutomatic = false,
        IsHitScan = true,
        SpawnInterval = 10,
        RecoilTime = 1
    };
    public static WeaponDefinition SmgDefinition = new WeaponDefinition
    {
        Type = WeaponType.Smg,
        MaxAmmo = 100,
        BulletsPerMagazine = 10,
        DamagePerBullet = 10,
        HeadShotDamagePerBullet = 20,
        ReloadTime = 1,
        ShotInterval = 0.1f,
        IsAutomatic = true,
        IsHitScan = true,
        SpawnInterval = 20,
        RecoilTime = 0.2f
    };
    public static WeaponDefinition ShotgunDefinition = new WeaponDefinition
    {
        Type = WeaponType.Shotgun,
        MaxAmmo = 64,
        BulletsPerMagazine = 8,
        DamagePerBullet = 10,
        HeadShotDamagePerBullet = 30,
        ReloadTime = 1,
        ShotInterval = 0.75f,
        IsAutomatic = false,
        IsHitScan = true,
        SpawnInterval = 40,
        RecoilTime = 0.6f
    };
    public static WeaponDefinition SniperRifleDefinition = new WeaponDefinition
    {
        Type = WeaponType.SniperRifle,
        MaxAmmo = 16,
        BulletsPerMagazine = 4,
        DamagePerBullet = MaxPlayerShield,
        HeadShotDamagePerBullet = 150,
        ReloadTime = 1,
        ShotInterval = 0.75f,
        IsAutomatic = false,
        IsHitScan = true,
        SpawnInterval = 60,
        RecoilTime = 1
    };
    public static WeaponDefinition RocketLauncherDefinition = new WeaponDefinition
    {
        Type = WeaponType.RocketLauncher,
        MaxAmmo = 8,
        BulletsPerMagazine = 2,
        DamagePerBullet = 200,
        HeadShotDamagePerBullet = 200,
        ReloadTime = 2,
        ShotInterval = 0.75f,
        IsAutomatic = false,
        IsHitScan = false,
        SpawnInterval = 40,
        RecoilTime = 2
    };
    public static WeaponDefinition GetWeaponDefinitionByType(WeaponType type)
    {
        switch (type)
        {
            case WeaponType.Pistol:
                return PistolDefinition;
            case WeaponType.Smg:
                return SmgDefinition;
            case WeaponType.RocketLauncher:
                return RocketLauncherDefinition;
            case WeaponType.Shotgun:
                return ShotgunDefinition;
            case WeaponType.SniperRifle:
                return SniperRifleDefinition;
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
        Damage = 130,
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
    public static CustomLogger Logger = new CustomLogger(Debug.unityLogger.logHandler);
    
    public Server Server;
    public Client Client;
    public string EnteredClientIpAddressAndPort;
    public bool IsInOptionsScreen;
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

    #region Inspector-set Variables
    public GameObject PlayerPrefab;
    public GameObject CameraPrefab;

    public GameObject PistolPrefab;
    public GameObject SmgPrefab;

    public GameObject RocketLauncherPrefab;
    public GameObject RocketPrefab;
    public GameObject RocketExplosionPrefab;

    public GameObject ShotgunPrefab;

    public GameObject SniperRiflePrefab;
    public Material SniperBulletTrailMaterial;

    public GameObject MuzzleFlashPrefab;

    public GameObject FragmentationGrenadePrefab;
    public GameObject FragmentationGrenadeExplosionPrefab;

    public GameObject StickyGrenadePrefab;
    public GameObject StickyGrenadeExplosionPrefab;

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

    public GameObject GetWeaponPrefab(WeaponType weaponType)
    {
        switch (weaponType)
        {
            case WeaponType.Pistol:
                return PistolPrefab;
            case WeaponType.Smg:
                return SmgPrefab;
            case WeaponType.RocketLauncher:
                return RocketLauncherPrefab;
            case WeaponType.Shotgun:
                return ShotgunPrefab;
            case WeaponType.SniperRifle:
                return SniperRiflePrefab;
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
        var weaponPrefab = GetWeaponPrefab(weaponObjectState.Type);
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

        CanvasObject = guiContainer.FindDescendant("Canvas");

        SetupRpcs();

        LoadSettings();
    }
    private void Start()
    {
        // Initialize & configure network.
        NetworkTransport.Init();

        EnteredClientIpAddressAndPort = LocalHostIpv4Address + ":" + Server.PortNumber;
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
            if (!IsInOptionsScreen)
            {
                RenderMainMenu();
            }
            else
            {
                RenderOptionsScreen();
            }
        }
        else
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
    private void SaveSettings()
    {
        System.IO.File.WriteAllText(SettingsFilePath, JsonUtility.ToJson(Settings));
    }

    private void RenderMainMenu()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        const float buttonWidth = 200;
        const float buttonHeight = 30;
        const float buttonSpacing = 10;
        const int buttonCount = 5;
        const float menuWidth = buttonWidth;
        const float menuHeight = (buttonCount * buttonHeight) + ((buttonCount - 1) * buttonSpacing);

        var position = new Vector2(
            (Screen.width / 2) - (menuWidth / 2),
            (Screen.height / 2) - (menuHeight / 2)
        );

        EnteredClientIpAddressAndPort = GUI.TextField(
            new Rect(position.x, position.y, buttonWidth, buttonHeight),
            EnteredClientIpAddressAndPort
        );
        position.y += buttonHeight + buttonSpacing;

        if (GUI.Button(new Rect(position.x, position.y, buttonWidth, buttonHeight), "Connect To Server"))
        {
            SceneManager.sceneLoaded += OnMapLoadedAsClient;
            SceneManager.LoadScene(SmallMapSceneName);
        }
        position.y += buttonHeight + buttonSpacing;

        if (GUI.Button(new Rect(position.x, position.y, buttonWidth, buttonHeight), "Start Server"))
        {
            Server = new Server();
            Server.Start();
        }
        position.y += buttonHeight + buttonSpacing;

        if (GUI.Button(new Rect(position.x, position.y, buttonWidth, buttonHeight), "Options"))
        {
            IsInOptionsScreen = true;
        }
        position.y += buttonHeight + buttonSpacing;
        
        if (GUI.Button(new Rect(position.x, position.y, buttonWidth, buttonHeight), "Quit"))
        {
            Application.Quit();
        }
        position.y += buttonHeight + buttonSpacing;
    }
    public void RenderOptionsScreen()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        const float buttonWidth = 200;
        const float buttonHeight = 30;
        const float buttonSpacing = 10;
        const int buttonCount = 3;
        const float menuWidth = buttonWidth;
        const float menuHeight = (buttonCount * buttonHeight) + ((buttonCount - 1) * buttonSpacing);

        var buttonSize = new Vector2(buttonWidth, buttonHeight);
        var position = new Vector2(
            (Screen.width / 2) - (menuWidth / 2),
            (Screen.height / 2) - (menuHeight / 2)
        );

        GUI.Label(
            new Rect(position - new Vector2(0, 25), buttonSize),
            $"Mouse Sensitivity: {OsFps.Instance.Settings.MouseSensitivity}"
        );
        Settings.MouseSensitivity = GUI.HorizontalSlider(
            new Rect(position, buttonSize), Settings.MouseSensitivity, MinMouseSensitivity, MaxMouseSensitivity
        );
        position.y += buttonHeight + buttonSpacing;

        GUI.Label(
            new Rect(position - new Vector2(0, 25), buttonSize),
            $"Field Of View: {OsFps.Instance.Settings.FieldOfViewY}"
        );
        Settings.FieldOfViewY = GUI.HorizontalSlider(
            new Rect(position, buttonSize), Settings.FieldOfViewY, MinFieldOfViewY, MaxFieldOfViewY
        );
        position.y += buttonHeight + buttonSpacing;

        if (GUI.Button(new Rect(position, buttonSize), "Ok"))
        {
            SaveSettings();
            IsInOptionsScreen = false;
        }
        position.y += buttonHeight + buttonSpacing;
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
    private void OnMapLoadedAsClient(Scene scene, LoadSceneMode loadSceneMode)
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
    }
}