using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Networking;
using NetworkLibrary;
using UnityEngine.Profiling;
using Newtonsoft.Json;
using Unity.Mathematics;

public class Client
{
    public const float SendPlayerInputInterval = 1.0f / 30;

    public ClientPeer ClientPeer;
    public uint? PlayerId;
    public GameObject Camera;
    public GameObject GuiContainer;
    public ChatBoxComponent ChatBox;
    public HealthBarComponent ShieldBar;
    public HealthBarComponent HealthBar;
    public int ZoomLevel;

    public event ClientPeer.ServerConnectionChangeEventHandler OnDisconnectedFromServer;

    public void Start(bool isServerRemote)
    {
        ClientPeer = new ClientPeer();
        ClientPeer.OnReceiveDataFromServer += OnReceiveDataFromServer;
        ClientPeer.OnConnectedToServer += InternalOnConnectedToServer;
        ClientPeer.OnDisconnectedFromServer += InternalOnDisconnectedFromServer;

        var connectionConfig = NetLib.CreateConnectionConfig(
            out reliableSequencedChannelId,
            out reliableChannelId,
            out unreliableStateUpdateChannelId,
            out unreliableFragmentedChannelId,
            out unreliableChannelId
        );
        ClientPeer.Start(connectionConfig);

        SendInputPeriodicFunction = new ThrottledAction(SendPlayerInput, SendPlayerInputInterval);

        Camera = Object.Instantiate(OsFps.Instance.CameraPrefab);

        CreateGui();
    }
    public void Stop()
    {
        ClientPeer.Stop();
        Object.Destroy(GuiContainer);
    }

    public void StartConnectingToServer(string serverIpv4Address, ushort serverPortNumber)
    {
        var networkError = ClientPeer.StartConnectingToServer(serverIpv4Address, serverPortNumber);

        if (networkError != NetworkError.Ok)
        {
            var errorMessage = string.Format(
                "Failed connecting to server {0}:{1}. Error: {2}",
                serverIpv4Address, serverPortNumber, networkError
            );
            OsFps.Logger.LogError(errorMessage);
        }
    }
    public void DisconnectFromServer()
    {
        var networkError = ClientPeer.DisconnectFromServer();

        if (networkError != NetworkError.Ok)
        {
            OsFps.Logger.LogError(string.Format("Failed disconnecting from server. Error: {0}", networkError));
        }
    }

    private System.Tuple<uint, byte[]> latestSequenceNumberDeltaGameStateBytesPair;
    public void Update()
    {
        Camera.GetComponent<Camera>().fieldOfView = GetCurrentFieldOfViewY();

        ClientPeer.Update();
        if (latestSequenceNumberDeltaGameStateBytesPair != null)
        {
            FinishHandlingDeltaGameState(
                latestSequenceNumberDeltaGameStateBytesPair.Item1,
                latestSequenceNumberDeltaGameStateBytesPair.Item2
            );
            latestSequenceNumberDeltaGameStateBytesPair = null;
        }

        if (ClientPeer.IsConnectedToServer)
        {
            if (PlayerId != null)
            {
                var playerObjectComponent = PlayerObjectSystem.Instance.FindPlayerObjectComponent(PlayerId.Value);

                if (Input.GetKeyDown(KeyCode.Alpha1))
                {
                    if (playerObjectComponent != null)
                    {
                        RequestSwitchWeapons(playerObjectComponent, 0);
                    }
                }

                if (Input.GetKeyDown(KeyCode.Alpha2))
                {
                    if (playerObjectComponent != null)
                    {
                        RequestSwitchWeapons(playerObjectComponent, 1);
                    }
                }

                var mouseScrollDirection = Input.GetAxis("Mouse ScrollWheel");
                if (mouseScrollDirection > 0)
                {
                    if (playerObjectComponent != null)
                    {
                        var newWeaponIndex = MathfExtensions.Wrap(
                            playerObjectComponent.State.CurrentWeaponIndex + 1,
                            0, playerObjectComponent.State.Weapons.Length - 1
                        );
                        RequestSwitchWeapons(
                            playerObjectComponent,
                            newWeaponIndex
                        );
                    }
                }
                else if (mouseScrollDirection < 0)
                {
                    if (playerObjectComponent != null)
                    {
                        var newWeaponIndex = MathfExtensions.Wrap(
                            playerObjectComponent.State.CurrentWeaponIndex - 1,
                            0, playerObjectComponent.State.Weapons.Length - 1
                        );
                        RequestSwitchWeapons(
                            playerObjectComponent,
                            newWeaponIndex
                        );
                    }
                }

                if (Input.GetButtonDown("Jump"))
                {
                    if ((playerObjectComponent != null) && PlayerObjectSystem.Instance.IsPlayerGrounded(playerObjectComponent))
                    {
                        PlayerObjectSystem.Instance.Jump(playerObjectComponent);
                        ClientPeer.CallRpcOnServer("ServerOnPlayerTryJump", reliableChannelId, new
                        {
                            playerId = PlayerId.Value
                        });
                    }
                }

                // Pickup Weapon
                if (playerObjectComponent != null)
                {
                    var playerId = playerObjectComponent.State.Id;
                    var playersClosestWeaponInfo = WeaponSystem.Instance.ClosestWeaponInfoByPlayerId
                        .GetValueOrDefault(playerId);

                    if (playersClosestWeaponInfo != null)
                    {
                        var closestWeaponId = playersClosestWeaponInfo.Item1;
                        var closestWeaponComponent = WeaponSystem.Instance.FindWeaponComponent(closestWeaponId);
                        if (closestWeaponComponent != null)
                        {
                            var closestWeaponType = closestWeaponComponent.State.Type;

                            var playersWeaponOfSameType = playerObjectComponent.State.Weapons.FirstOrDefault(
                                w => w?.Type == closestWeaponType
                            );
                            var playerHasWeaponOfTypeWithRoomForAmmo =
                                (playersWeaponOfSameType != null) &&
                                (playersWeaponOfSameType.BulletsUsed > 0);
                            var playerHasEmptyWeaponSlot = playerObjectComponent.State.Weapons.Any(
                                w => w == null
                            );

                            if (playerHasWeaponOfTypeWithRoomForAmmo || playerHasEmptyWeaponSlot || Input.GetButtonDown("Pickup Weapon"))
                            {
                                ClientPeer.CallRpcOnServer("ServerOnPlayerTryPickupWeapon", reliableChannelId, new
                                {
                                    playerId = playerId,
                                    weaponId = closestWeaponId
                                });
                            }
                        }
                    }
                }

                if (Input.GetButtonDown("Zoom"))
                {
                    ChangeZoomLevel();

                    var equippedWeaponComponent = GetEquippedWeaponComponent(playerObjectComponent);
                    var weaponAudioSource = equippedWeaponComponent.GetComponent<AudioSource>();
                    weaponAudioSource?.PlayOneShot(OsFps.Instance.SniperZoomSound);
                }

                if (playerObjectComponent?.State.IsAlive ?? false)
                {
                    ShieldBar.gameObject.SetActive(true);

                    var shieldPercent = playerObjectComponent.State.Shield / OsFps.MaxPlayerShield;
                    ShieldBar.HealthPercent = shieldPercent;

                    HealthBar.gameObject.SetActive(true);

                    var healthPercent = playerObjectComponent.State.Health / OsFps.MaxPlayerHealth;
                    HealthBar.HealthPercent = healthPercent;
                }
                else
                {
                    ShieldBar.gameObject.SetActive(false);
                    HealthBar.gameObject.SetActive(false);
                }
            }

            if (Input.GetButtonDown("Chat"))
            {
                if (!_isShowingChatMessageInput)
                {
                    SetChatBoxIsVisible(true);
                    ChatBox.MessageInputField.Select();
                    ChatBox.MessageInputField.ActivateInputField();

                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
                else
                {
                    ConfirmChatMessage();
                }
            }

            if (Input.GetButtonDown("Toggle Menu"))
            {
                if (!_isShowingMenu)
                {
                    var pauseScreenComponent = GameObject.Instantiate(
                        OsFps.Instance.PauseScreenPrefab, OsFps.Instance.CanvasObject.transform
                    ).GetComponent<PauseScreenComponent>();
                    OsFps.Instance.MenuStack.Push(pauseScreenComponent);

                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
                else
                {
                    OsFps.Instance.MenuStack.Pop();
                }
            }

            if (!_isShowingChatMessageInput && !_isShowingMenu)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            var chatMessagesText = string.Join("\n", _chatMessages);
            ChatBox.VisualMessagesText.text = chatMessagesText;
            ChatBox.ScrollableMessagesText.text = chatMessagesText;

            SendInputPeriodicFunction.TryToCall();

            if (PlayerId != null)
            {
                if (!OsFps.Instance.IsRemoteClient && (Camera.transform.parent == null))
                {
                    var playerObjectComponent = PlayerObjectSystem.Instance.FindPlayerObjectComponent(PlayerId.Value);
                    if (playerObjectComponent != null)
                    {
                        AttachCameraToPlayer(PlayerId.Value);
                    }
                }
            }
        }
    }
    public void LateUpdate()
    {
        if (PlayerId != null)
        {
            var playerObjectComponent = PlayerObjectSystem.Instance.FindPlayerObjectComponent(PlayerId.Value);

            if (playerObjectComponent != null)
            {
                var playerObjectState = playerObjectComponent.State;
                playerObjectState.Position = playerObjectComponent.transform.position;
                playerObjectState.Velocity = playerObjectComponent.Rigidbody.velocity;
                playerObjectState.LookDirAngles = PlayerObjectSystem.Instance.GetPlayerLookDirAngles(playerObjectComponent);
            }
        }
    }

    public void LeaveServer()
    {
        if (ClientPeer.IsConnectedToServer)
        {
            DisconnectFromServer();
        }
        else
        {
            InternalOnDisconnectedFromServer();
        }
    }

    #region GUI
    private void CreateGui()
    {
        GuiContainer = new GameObject("GUI Container");
        GuiContainer.transform.SetParent(OsFps.Instance.CanvasObject.transform);
        GuiContainer.transform.localPosition = Vector3.zero;
        GuiContainer.transform.localRotation = Quaternion.identity;
        var rectTransform = GuiContainer.AddComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;

        GameObject crosshair = Object.Instantiate(OsFps.Instance.CrosshairPrefab);
        crosshair.transform.SetParent(GuiContainer.transform);
        crosshair.transform.localPosition = Vector3.zero;
        crosshair.transform.localRotation = Quaternion.identity;

        ChatBox = Object.Instantiate(OsFps.Instance.ChatBoxPrefab).GetComponent<ChatBoxComponent>();
        ChatBox.transform.SetParent(GuiContainer.transform, worldPositionStays: false);
        SetChatBoxIsVisible(false);

        ShieldBar = Object.Instantiate(OsFps.Instance.HealthBarPrefab).GetComponent<HealthBarComponent>();
        ShieldBar.transform.SetParent(GuiContainer.transform, worldPositionStays: false);
        var shieldBarRectTransform = ShieldBar.GetComponent<RectTransform>();
        shieldBarRectTransform.anchorMin = new Vector2(0.5f, 1);
        shieldBarRectTransform.anchorMax = new Vector2(0.5f, 1);
        shieldBarRectTransform.pivot = new Vector2(0.5f, 1);
        shieldBarRectTransform.anchoredPosition = new Vector2(0, -15);

        HealthBar = Object.Instantiate(OsFps.Instance.HealthBarPrefab).GetComponent<HealthBarComponent>();
        HealthBar.transform.SetParent(GuiContainer.transform, worldPositionStays: false);
        var healthBarRectTransform = HealthBar.GetComponent<RectTransform>();
        healthBarRectTransform.anchorMin = new Vector2(0.5f, 1);
        healthBarRectTransform.anchorMax = new Vector2(0.5f, 1);
        healthBarRectTransform.pivot = new Vector2(0.5f, 1);
        healthBarRectTransform.anchoredPosition = new Vector2(0, -40);
        HealthBar.Color = new Color32(253, 74, 74, 255);
    }

    private void SetChatBoxIsVisible(bool isVisible)
    {
        ChatBox.MessagesScrollView.gameObject.SetActive(isVisible);
        ChatBox.MessageInputField.gameObject.SetActive(isVisible);
        ChatBox.VisualMessagesText.gameObject.SetActive(!isVisible);
    }
    public void OnGui()
    {
        if (ClientPeer.IsConnectedToServer)
        {
            if (_isShowingConnectingScreen)
            {
                OsFps.Instance.MenuStack.Pop();
            }

            DrawHud();

            if (Input.GetButton("Show Scoreboard"))
            {
                DrawScoreBoard();
            }

            if (PlayerId.HasValue)
            {
                var closestWeaponInfo = WeaponSystem.Instance.ClosestWeaponInfoByPlayerId
                    .GetValueOrDefault(PlayerId.Value);

                if (closestWeaponInfo != null)
                {
                    var weaponComponent = WeaponSystem.Instance.FindWeaponComponent(closestWeaponInfo.Item1);

                    if (weaponComponent != null)
                    {
                        DrawWeaponPickupHud(weaponComponent);
                    }
                }
            }
        }
        else
        {
            if (!_isShowingConnectingScreen)
            {
                var connectingScreenComponent = GameObject.Instantiate(
                    OsFps.Instance.ConnectingScreenPrefab, OsFps.Instance.CanvasObject.transform
                ).GetComponent<ConnectingScreenComponent>();
                OsFps.Instance.MenuStack.Push(connectingScreenComponent);
            }
        }
    }
    private void DrawHud()
    {
        const float hudMargin = 10;

        var playerObjectComponent = PlayerId.HasValue
            ? PlayerObjectSystem.Instance.FindPlayerObjectComponent(PlayerId.Value)
            : null;
        if (playerObjectComponent == null) return;

        var playerObjectState = playerObjectComponent.State;

        var weaponHudPosition = new Vector2(hudMargin, hudMargin);
        var weaponIconSize = new Vector2(64, 64);
        var weaponIconSpacingY = weaponIconSize.y + 30;
        if (playerObjectState.CurrentWeapon != null)
        {
            DrawWeaponHud(playerObjectState.CurrentWeapon, weaponHudPosition, weaponIconSize);
            weaponHudPosition.y += weaponIconSpacingY;
        }

        foreach (var weapon in playerObjectState.Weapons)
        {
            if ((weapon != null) && (weapon != playerObjectState.CurrentWeapon))
            {
                DrawWeaponHud(weapon, weaponHudPosition, weaponIconSize);
                weaponHudPosition.y += weaponIconSpacingY;
            }
        }

        var grenadeIconSize = new Vector2(48, 48);
        var grenadeHudPosition = new Vector2(Screen.width - hudMargin - grenadeIconSize.x, hudMargin);
        var grenadeIconSpacingY = grenadeIconSize.y + 30;
        if (playerObjectState.CurrentGrenadeSlot != null)
        {
            DrawGrenadeSlotHud(playerObjectState.CurrentGrenadeSlot, grenadeHudPosition, grenadeIconSize);
            grenadeHudPosition.y += grenadeIconSpacingY;
        }

        foreach (var grenadeSlot in playerObjectState.GrenadeSlots)
        {
            if ((grenadeSlot != null) && (grenadeSlot != playerObjectState.CurrentGrenadeSlot))
            {
                DrawGrenadeSlotHud(grenadeSlot, grenadeHudPosition, grenadeIconSize);
                grenadeHudPosition.y += grenadeIconSpacingY;
            }
        }
    }
    private void DrawNetworkStats()
    {
        var networkStats = ClientPeer.GetNetworkStats(ClientPeer.serverConnectionId);
        var position = new Vector2(30, 30);
        GUI.Label(new Rect(position, new Vector2(800, 800)), JsonConvert.SerializeObject(networkStats));
    }
    private void DrawWeaponHud(EquippedWeaponState weapon, Vector2 position, Vector2 iconSize)
    {
        GUI.DrawTexture(new Rect(position, iconSize), weapon.Definition.Icon);
        GUI.Label(
            new Rect(position + new Vector2(0, iconSize.y), new Vector2(260, 50)),
            weapon.BulletsLeftInMagazine + " / " + weapon.BulletsLeftOutOfMagazine
        );
    }
    private void DrawGrenadeSlotHud(GrenadeSlot grenadeSlot, Vector2 position, Vector2 iconSize)
    {
        var grenadeDefinition = GrenadeSystem.Instance.GetGrenadeDefinitionByType(grenadeSlot.GrenadeType);
        GUI.DrawTexture(new Rect(position, iconSize), grenadeDefinition.Icon);
        GUI.Label(
            new Rect(position + new Vector2(0, iconSize.y), new Vector2(260, 50)),
            grenadeSlot.GrenadeCount + " / " + OsFps.MaxGrenadesPerType
        );
    }
    private void DrawScoreBoard()
    {
        const float playerIdColumnWidth = 50;
        const float pingColumnWidth = 100;
        const float killsColumnWidth = 100;
        const float deathsColumnWidth = 100;
        const float scoreBoardWidth = playerIdColumnWidth + pingColumnWidth + killsColumnWidth + deathsColumnWidth;
        const float scoreBoardPadding = 10;
        const float rowHeight = 30;

        var playerComponents = Object.FindObjectsOfType<PlayerComponent>();

        var scoreBoardHeight = rowHeight * (1 + playerComponents.Length);

        var boxWidth = scoreBoardWidth + scoreBoardPadding;
        var boxHeight = scoreBoardHeight + scoreBoardPadding;
        GUI.Box(
            new Rect(
                (Screen.width / 2) - (boxWidth / 2),
                (Screen.height / 2) - (boxHeight / 2),
                boxWidth,
                boxHeight
            ),
            ""
        );

        var position = new Vector2(
            (Screen.width / 2) - (scoreBoardWidth / 2),
            (Screen.height / 2) - (scoreBoardHeight / 2)
        );
        var idColumnX = position.x;
        var pingColumnX = idColumnX + pingColumnWidth;
        var killsColumnX = pingColumnX + playerIdColumnWidth;
        var deathsColumnX = killsColumnX + killsColumnWidth;

        // Draw table header.
        GUI.Label(new Rect(idColumnX, position.y, playerIdColumnWidth, rowHeight), "Player");
        GUI.Label(new Rect(pingColumnX, position.y, pingColumnWidth, rowHeight), "Ping");
        GUI.Label(new Rect(killsColumnX, position.y, killsColumnWidth, rowHeight), "Kills");
        GUI.Label(new Rect(deathsColumnX, position.y, deathsColumnWidth, rowHeight), "Deaths");
        position.y += rowHeight;

        // Draw player rows.
        foreach (var playerComponent in playerComponents)
        {
            var playerState = playerComponent.State;
            var pingInMilliseconds = playerState.RoundTripTimeInMilliseconds;
            var pingString = pingInMilliseconds.ToString();

            GUI.Label(new Rect(idColumnX, position.y, playerIdColumnWidth, rowHeight), playerState.Name.ToString());
            GUI.Label(new Rect(pingColumnX, position.y, pingColumnWidth, rowHeight), pingString);
            GUI.Label(new Rect(killsColumnX, position.y, killsColumnWidth, rowHeight), playerState.Kills.ToString());
            GUI.Label(new Rect(deathsColumnX, position.y, deathsColumnWidth, rowHeight), playerState.Deaths.ToString());
            position.y += rowHeight;
        }
    }
    private void DrawWeaponPickupHud(WeaponComponent weaponComponent)
    {
        const float margin = 10;
        var labelSize = new Vector2(300, 30);
        var labelPosition = new Vector2(Screen.width - margin - labelSize.x, Screen.height - margin - labelSize.y);
        var text = $"Press E to pick up {weaponComponent.State.Type}.";

        GUI.Label(new Rect(labelPosition, labelSize), text);
    }
    #endregion

    public int reliableSequencedChannelId;
    public int reliableChannelId;
    public int unreliableStateUpdateChannelId;
    public int unreliableFragmentedChannelId;
    public int unreliableChannelId;

    private ThrottledAction SendInputPeriodicFunction;

    public bool _isShowingChatMessageInput
    {
        get
        {
            return ChatBox?.MessageInputField.gameObject.activeSelf ?? false;
        }
    }
    public List<string> _chatMessages = new List<string>();

    public bool _isShowingMenu
    {
        get
        {
            return
                OsFps.Instance.MenuStack.Any() &&
                (OsFps.Instance.MenuStack.Any(mc => mc is PauseScreenComponent));
        }
    }
    public bool _isShowingConnectingScreen
    {
        get
        {
            return
                OsFps.Instance.MenuStack.Any() &&
                (OsFps.Instance.MenuStack.Any(mc => mc is ConnectingScreenComponent));
        }
    }

    private float GetCurrentFieldOfViewY()
    {
        switch (ZoomLevel)
        {
            case 0:
                return OsFps.Instance.Settings.FieldOfViewY;
            case 1:
                return 30;
            case 2:
                return 15;
            default:
                throw new System.NotImplementedException("Unimplemented zoom level.");
        }
    }
    private bool CanZoomIn()
    {
        if (ZoomLevel == 2) return false;

        if (PlayerId == null) return false;

        var playerObjectComponent = PlayerObjectSystem.Instance.FindPlayerObjectComponent(PlayerId.Value);
        if (playerObjectComponent == null) return false;

        var currentWeapon = playerObjectComponent.State.CurrentWeapon;
        if (currentWeapon == null) return false;

        return currentWeapon.Type == WeaponType.SniperRifle;
    }
    private void ChangeZoomLevel()
    {
        if (PlayerId == null) return;

        if (CanZoomIn())
        {
            ZoomLevel++;
        }
        else
        {
            ZoomLevel = 0;
        }
    }
    public float GetMouseSensitivityMultiplierForZoomLevel()
    {
        switch (ZoomLevel)
        {
            case 0:
                return 1;
            case 1:
                return 0.5f;
            case 2:
                return 0.25f;
            default:
                throw new System.NotImplementedException("Unimplemented zoom level.");
        }
    }
    public float GetMouseSensitivityForZoomLevel()
    {
        return GetMouseSensitivityMultiplierForZoomLevel() * OsFps.Instance.Settings.MouseSensitivity;
    }
    private void AttachCameraToPlayer(uint playerId)
    {
        var playerObject = PlayerObjectSystem.Instance.FindPlayerObject(playerId);
        var cameraPointObject = playerObject.GetComponent<PlayerObjectComponent>().CameraPointObject;

        Camera.transform.SetParent(cameraPointObject.transform);

        Camera.transform.localPosition = Vector3.zero;
        Camera.transform.localRotation = Quaternion.identity;
    }
    public void DetachCameraFromPlayer()
    {
        Camera.transform.SetParent(null, true);
    }
    public EquippedWeaponComponent GetEquippedWeaponComponent(PlayerObjectComponent playerObjectComponent)
    {
        foreach (Transform weaponTransform in playerObjectComponent.HandsPointObject.transform)
        {
            var equippedWeaponComponent = weaponTransform.gameObject.GetComponent<EquippedWeaponComponent>();
            if (equippedWeaponComponent != null)
            {
                return equippedWeaponComponent;
            }
        }

        return null;
    }
    public void VisualEquipWeapon(PlayerObjectState playerObjectState)
    {
        var playerObjectComponent = PlayerObjectSystem.Instance.FindPlayerObjectComponent(playerObjectState.Id);

        var equippedWeaponComponent = GetEquippedWeaponComponent(playerObjectComponent);
        var wasEQCNull = equippedWeaponComponent == null;
        if (equippedWeaponComponent != null)
        {
            Object.DestroyImmediate(equippedWeaponComponent.gameObject);
        }

        if (playerObjectState.CurrentWeapon != null)
        {
            var weaponPrefab = WeaponSystem.Instance.GetWeaponDefinitionByType(playerObjectState.CurrentWeapon.Type).Prefab;

            GameObject weaponObject = Object.Instantiate(weaponPrefab, Vector3.zero, Quaternion.identity);
            var animator = weaponObject.AddComponent<Animator>();
            animator.runtimeAnimatorController = OsFps.Instance.RecoilAnimatorController;

            weaponObject.transform.SetParent(playerObjectComponent.HandsPointObject.transform, false);

            var weaponComponent = weaponObject.GetComponent<WeaponComponent>();
            Object.DestroyImmediate(weaponComponent.Rigidbody);
            Object.DestroyImmediate(weaponComponent.Collider);
            Object.DestroyImmediate(weaponComponent);

            playerObjectState.EquipWeaponTimeLeft = OsFps.EquipWeaponTime;
            playerObjectState.ReloadTimeLeft = -1;
            playerObjectState.RecoilTimeLeft = -1;

            equippedWeaponComponent = weaponObject.AddComponent<EquippedWeaponComponent>();
            equippedWeaponComponent.State = playerObjectState.CurrentWeapon;
            equippedWeaponComponent.State.TimeSinceLastShot = equippedWeaponComponent.State.Definition.ShotInterval;
            equippedWeaponComponent.Animator = animator;

            animator.Play("Equip");
        }

        var weaponCount = 0;
        foreach (Transform weaponTransform in playerObjectComponent.HandsPointObject.transform)
        {
            weaponCount++;
        }

        ZoomLevel = 0;
    }
    public void Reload(PlayerObjectComponent playerObjectComponent)
    {
        ClientPeer.CallRpcOnServer("ServerOnPlayerReloadPressed", reliableChannelId, new
        {
            playerId = playerObjectComponent.State.Id
        });

        playerObjectComponent.State.ReloadTimeLeft = playerObjectComponent.State.CurrentWeapon.Definition.ReloadTime;
        playerObjectComponent.State.RecoilTimeLeft = -1;

        var equippedWeaponComponent = GetEquippedWeaponComponent(playerObjectComponent);

        var audioSource = equippedWeaponComponent?.GetComponent<AudioSource>();
        audioSource?.PlayOneShot(OsFps.Instance.ReloadSound);

        equippedWeaponComponent.Animator.Play("Reload");
    }

    public void ShowMuzzleFlash(PlayerObjectComponent playerObjectComponent)
    {
        GameObject muzzleFlashObject = Object.Instantiate(
            OsFps.Instance.MuzzleFlashPrefab, Vector3.zero, Quaternion.identity
        );
        var barrelExitObject = playerObjectComponent.HandsPointObject.FindDescendant("BarrelExit");
        muzzleFlashObject.transform.SetParent(barrelExitObject.transform, false);

        Object.Destroy(muzzleFlashObject, OsFps.MuzzleFlashDuration);
    }
    public void ShowWeaponFireEffects(PlayerObjectComponent playerObjectComponent, Ray aimRay)
    {
        ShowMuzzleFlash(playerObjectComponent);

        var weapon = playerObjectComponent.State.CurrentWeapon;
        if (weapon != null)
        {
            if (weapon.Type == WeaponType.SniperRifle)
            {
                WeaponSystem.Instance.CreateSniperBulletTrail(aimRay);
            }

            var equippedWeaponComponent = GetEquippedWeaponComponent(playerObjectComponent);
            if (equippedWeaponComponent != null)
            {
                var weaponAudioSource = equippedWeaponComponent.GetComponent<AudioSource>();
                weaponAudioSource?.PlayOneShot(weapon.Definition.ShotSound);

                equippedWeaponComponent.Animator.Play("Recoil");
            }

            if (weapon.Definition.IsHitScan)
            {
                foreach (var shotRay in WeaponSystem.Instance.ShotRays(weapon.Definition, aimRay))
                {
                    CreateBulletHole(playerObjectComponent, shotRay);
                }
            }
        }
    }
    private void CreateBulletHole(PlayerObjectComponent playerObjectComponent, Ray shotRay)
    {
        var possibleHit = WeaponSystem.Instance.GetClosestValidRaycastHitForGunShot(shotRay, playerObjectComponent);

        if (possibleHit != null)
        {
            var raycastHit = possibleHit.Value;
            var bulletHolePosition = raycastHit.point + (0.01f * raycastHit.normal);
            var bulletHoleOrientation = Quaternion.LookRotation(-raycastHit.normal);
            var bulletHole = Object.Instantiate(
                OsFps.Instance.BulletHolePrefab, bulletHolePosition, bulletHoleOrientation, raycastHit.transform
            );
            Object.Destroy(bulletHole, 5);
        }
    }
    public void ShowGrenadeExplosion(Vector3 position, GrenadeType grenadeType)
    {
        var explosionPrefab = GrenadeSystem.Instance.GetGrenadeDefinitionByType(grenadeType).ExplosionPrefab;
        GameObject grenadeExplosionObject = Object.Instantiate(
            explosionPrefab, position, Quaternion.identity
        );

        var audioSource = grenadeExplosionObject.GetComponent<AudioSource>();
        audioSource?.Play();

        Object.Destroy(grenadeExplosionObject, OsFps.GrenadeExplosionDuration);
    }
    public void ShowRocketExplosion(Vector3 position)
    {
        var explosionPrefab = OsFps.Instance.RocketExplosionPrefab;
        GameObject explosionObject = Object.Instantiate(
            explosionPrefab, position, Quaternion.identity
        );

        var audioSource = explosionObject.GetComponent<AudioSource>();
        audioSource?.Play();

        Object.Destroy(explosionObject, OsFps.RocketExplosionDuration);
    }
    public void PlayerTryToShoot(PlayerObjectComponent playerObjectComponent)
    {
        if (playerObjectComponent.State.CanShoot)
        {
            ClientPeer.CallRpcOnServer("ServerOnPlayerTriggerPulled", reliableChannelId, new
            {
                playerId = playerObjectComponent.State.Id,
                shotRay = PlayerObjectSystem.Instance.GetShotRay(playerObjectComponent)
            });

            // predict the shot
            var shotRay = PlayerObjectSystem.Instance.GetShotRay(playerObjectComponent);
            ShowWeaponFireEffects(playerObjectComponent, shotRay);

            playerObjectComponent.State.RecoilTimeLeft = playerObjectComponent.State.CurrentWeapon.Definition.RecoilTime;
        }
        else
        {
            var equippedWeaponComponent = GetEquippedWeaponComponent(playerObjectComponent);
            var weaponAudioSource = equippedWeaponComponent.GetComponent<AudioSource>();
            weaponAudioSource?.PlayOneShot(OsFps.Instance.GunDryFireSound);
        }

        playerObjectComponent.State.CurrentWeapon.TimeSinceLastShot = 0;
    }

    public void ThrowGrenade(PlayerObjectState playerObjectState)
    {
        ClientPeer.CallRpcOnServer("ServerOnPlayerThrowGrenade", reliableChannelId, new
        {
            playerId = playerObjectState.Id
        });

        playerObjectState.TimeUntilCanThrowGrenade = OsFps.GrenadeThrowInterval;
    }
    public void SwitchGrenadeType(PlayerObjectState playerObjectState)
    {
        ClientPeer.CallRpcOnServer("ServerOnPlayerSwitchGrenadeType", reliableChannelId, new {
            playerId = playerObjectState.Id
        });
    }

    public void RequestSwitchWeapons(PlayerObjectComponent playerObjectComponent, int weaponIndex)
    {
        Assert.IsNotNull(playerObjectComponent);

        var playerObjectState = playerObjectComponent.State;

        if (weaponIndex == playerObjectState.CurrentWeaponIndex) return;

        // Send message to server.
        ClientPeer.CallRpcOnServer("ServerOnChangeWeapon", reliableSequencedChannelId, new
        {
            playerId = playerObjectState.Id,
            weaponIndex = (byte)weaponIndex
        });
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
        return serverEulerAngles;
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

    private void ConfirmChatMessage()
    {
        if (ChatBox.MessageInputField.text.Length > 0)
        {
            ClientPeer.CallRpcOnServer("ServerOnChatMessage", reliableChannelId, new
            {
                playerId = PlayerId,
                message = ChatBox.MessageInputField.text
            });

            ChatBox.MessageInputField.text = "";
        }

        SetChatBoxIsVisible(false);
    }

    private void GetChanges<T>(
        List<T> oldList,
        List<T> newList,
        System.Func<T, T, bool> doElementsMatch,
        out List<T> removedElements,
        out List<T> addedElements,
        out List<T> updatedElements
    )
    {
        removedElements = oldList.Where(oldElement =>
            !newList.Any(newElement => doElementsMatch(oldElement, newElement))
        ).ToList();
        addedElements = newList.Where(newElement =>
            !oldList.Any(oldElement => doElementsMatch(oldElement, newElement))
        ).ToList();
        updatedElements = newList.Where(newElement =>
            oldList.Any(oldElement => doElementsMatch(oldElement, newElement))
        ).ToList();
    }

    private void ApplyStates<StateType>(
        List<StateType> oldStates, List<StateType> newStates,
        System.Func<StateType, StateType, bool> doStatesHaveSameId,
        System.Action<StateType> removeStateObject,
        System.Action<StateType> addStateObject,
        System.Action<StateType, StateType> updateStateObject
    )
    {
        List<StateType> removedStates, addedStates, updatedStates;
        GetChanges(
            oldStates, newStates, doStatesHaveSameId,
            out removedStates, out addedStates, out updatedStates
        );

        // Despawn weapon objects.
        foreach (var removedState in removedStates)
        {
            removeStateObject(removedState);
        }

        // Spawn weapon objects.
        foreach (var addedState in addedStates)
        {
            addStateObject(addedState);
        }

        // Update existing weapon objects.
        foreach (var updatedState in updatedStates)
        {
            var oldState = oldStates.First(os => doStatesHaveSameId(os, updatedState));
            updateStateObject(oldState, updatedState);
        }
    }

    public GameObject CreateGameObjectFromState(object state)
    {
        var stateType = state.GetType();

        if (stateType.IsEquivalentTo(typeof(PlayerState)))
        {
            return PlayerObjectSystem.Instance.CreateLocalPlayerDataObject((PlayerState)state);
        }
        else if (stateType.IsEquivalentTo(typeof(PlayerObjectState)))
        {
            return SpawnPlayer((PlayerObjectState)state);
        }
        else if (stateType.IsEquivalentTo(typeof(WeaponObjectState)))
        {
            return WeaponSpawnerSystem.Instance.SpawnLocalWeaponObject((WeaponObjectState)state);
        }
        else if (stateType.IsEquivalentTo(typeof(WeaponSpawnerState)))
        {
            var weaponSpawner = Object.Instantiate(OsFps.Instance.WeaponSpawnerPrefab);
            var weaponSpawnerComponent = weaponSpawner.GetComponent<WeaponSpawnerComponent>();
            weaponSpawnerComponent.State = (WeaponSpawnerState)state;

            return weaponSpawner;
        }
        else if (stateType.IsEquivalentTo(typeof(GrenadeState)))
        {
            return GrenadeSpawnerSystem.Instance.SpawnLocalGrenadeObject((GrenadeState)state);
        }
        else if (stateType.IsEquivalentTo(typeof(GrenadeSpawnerState)))
        {
            var grenadeSpawner = Object.Instantiate(OsFps.Instance.GrenadeSpawnerPrefab);
            var grenadeSpawnerComponent = grenadeSpawner.GetComponent<GrenadeSpawnerComponent>();
            grenadeSpawnerComponent.State = (GrenadeSpawnerState)state;

            return grenadeSpawner;
        }
        else if (stateType.IsEquivalentTo(typeof(RocketState)))
        {
            return RocketSystem.Instance.SpawnLocalRocketObject((RocketState)state);
        }
        else
        {
            throw new System.NotImplementedException();
        }
    }

    private void ApplyState(
        NetworkedComponentTypeInfo networkedComponentTypeInfo, List<object> oldStates, List<object> newStates
    )
    {
        System.Func<object, object, bool> doIdsMatch =
            (s1, s2) => NetLib.GetIdFromState(networkedComponentTypeInfo, s1) == NetLib.GetIdFromState(networkedComponentTypeInfo, s2);

        System.Action<object> handleRemovedState = removedState =>
        {
            var monoBehaviour = NetLib.GetMonoBehaviourByState(networkedComponentTypeInfo, removedState);
            Object.Destroy(monoBehaviour.gameObject);
        };

        System.Action<object> handleAddedState = addedState =>
        {
            var gameObject = CreateGameObjectFromState(addedState);

            if (
                (networkedComponentTypeInfo != null) &&
                (networkedComponentTypeInfo.MonoBehaviourApplyStateMethod != null)
            )
            {
                var stateId = NetLib.GetIdFromState(networkedComponentTypeInfo, addedState);
                var monoBehaviour = NetLib.GetMonoBehaviourByStateId(networkedComponentTypeInfo, stateId);

                networkedComponentTypeInfo.MonoBehaviourApplyStateMethod.Invoke(monoBehaviour, new[] { addedState });
            }
        };

        System.Action<object, object> handleUpdatedState =
            (oldState, newState) =>
            {
                var oldStateId = NetLib.GetIdFromState(networkedComponentTypeInfo, oldState);
                var monoBehaviour = NetLib.GetMonoBehaviourByStateId(networkedComponentTypeInfo, oldStateId);

                if (networkedComponentTypeInfo.MonoBehaviourApplyStateMethod == null)
                {
                    networkedComponentTypeInfo.MonoBehaviourStateField.SetValue(monoBehaviour, newState);
                }
                else
                {
                    networkedComponentTypeInfo.MonoBehaviourApplyStateMethod?.Invoke(monoBehaviour, new[] { newState });
                }
            };

        ApplyStates(
            oldStates, newStates, doIdsMatch,
            handleRemovedState, handleAddedState, handleUpdatedState
        );
    }

    private GameObject SpawnPlayer(PlayerObjectState playerState)
    {
        var playerObject = PlayerRespawnSystem.Instance.SpawnLocalPlayer(playerState);

        if (playerState.Id == PlayerId)
        {
            AttachCameraToPlayer(playerState.Id);
            HidePlayerModelFromCamera(playerObject);
        }

        return playerObject;
    }
    private void HidePlayerModelFromCamera(GameObject playerObject)
    {
        var modelContainer = playerObject.FindDescendant("Model");

        foreach (var descendantTransform in modelContainer.transform.ThisAndDescendantsDepthFirst())
        {
            var meshRenderer = descendantTransform.GetComponent<MeshRenderer>();
            if (meshRenderer != null)
            {
                meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
            }
        }
    }

    #region Message Handlers
    private void OnReceiveDataFromServer(int channelId, byte[] bytesReceived, int numBytesReceived)
    {
        Profiler.BeginSample("OnReceiveDataFromServer");
        using (var memoryStream = new MemoryStream(bytesReceived, 0, numBytesReceived))
        {
            using (var reader = new BinaryReader(memoryStream))
            {
                var messageTypeAsByte = reader.ReadByte();
                RpcInfo rpcInfo;

                if (messageTypeAsByte == NetLib.StateSynchronizationMessageId)
                {
                    OnReceiveDeltaGameStateFromServer(reader, bytesReceived, numBytesReceived);
                }
                else if (NetLib.rpcInfoById.TryGetValue(messageTypeAsByte, out rpcInfo))
                {
                    Profiler.BeginSample("Deserialize & Execute RPC");
                    var rpcArguments = NetworkSerializationUtils.DeserializeRpcCallArguments(rpcInfo, reader);
                    NetLib.ExecuteRpc(rpcInfo.Id, null, this, rpcArguments);
                    Profiler.EndSample();
                }
                else
                {
                    throw new System.NotImplementedException("Unknown message type: " + messageTypeAsByte);
                }
            }
        }
        Profiler.EndSample();
    }

    private List<NetworkedGameState> cachedReceivedGameStates = new List<NetworkedGameState>();
    private void OnReceiveDeltaGameStateFromServer(BinaryReader reader, byte[] bytesReceived, int numBytesReceived)
    {
        uint latestReceivedGameStateSequenceNumber = (cachedReceivedGameStates.Any())
            ? cachedReceivedGameStates[cachedReceivedGameStates.Count - 1].SequenceNumber
            : 0;
        latestReceivedGameStateSequenceNumber = System.Math.Max(
            latestReceivedGameStateSequenceNumber,
            latestSequenceNumberDeltaGameStateBytesPair?.Item1 ?? 0
        );

        var sequenceNumber = reader.ReadUInt32();
        if (sequenceNumber > latestReceivedGameStateSequenceNumber)
        {
            var bytesLeft = new byte[numBytesReceived - reader.BaseStream.Position];
            
            System.Array.Copy(bytesReceived, reader.BaseStream.Position, bytesLeft, 0, bytesLeft.Length);
            latestSequenceNumberDeltaGameStateBytesPair = new System.Tuple<uint, byte[]>(
                sequenceNumber,
                bytesLeft
            );
        }
    }
    private void FinishHandlingDeltaGameState(uint sequenceNumber, byte[] deltaBytes)
    {
        using (var memoryStream = new MemoryStream(deltaBytes))
        {
            using (var reader = new BinaryReader(memoryStream))
            {
                var sequenceNumberRelativeTo = reader.ReadUInt32();
                var networkedGameStateRelativeTo = GetNetworkedGameStateRelativeTo(sequenceNumberRelativeTo);

                Profiler.BeginSample("State Deserialization");
                var receivedGameState = NetworkSerializationUtils.DeserializeNetworkedGameState(
                    reader, sequenceNumber, networkedGameStateRelativeTo
                );
                Profiler.EndSample();

                ClientPeer.CallRpcOnServer("ServerOnReceiveClientGameStateAck", unreliableChannelId, new
                {
                    playerId = PlayerId.Value,
                    gameStateSequenceNumber = receivedGameState.SequenceNumber
                });

                cachedReceivedGameStates.Add(receivedGameState);

                var indexOfLatestGameStateToDiscard = cachedReceivedGameStates
                    .FindLastIndex(ngs => ngs.SequenceNumber < sequenceNumberRelativeTo);
                if (indexOfLatestGameStateToDiscard >= 0)
                {
                    var numberOfLatestGameStatesToDiscard = indexOfLatestGameStateToDiscard + 1;
                    cachedReceivedGameStates.RemoveRange(0, numberOfLatestGameStatesToDiscard);
                }

                Profiler.BeginSample("ClientOnReceiveGameState");
                ClientOnReceiveGameState(receivedGameState);
                Profiler.EndSample();
            }
        }
    }
    private NetworkedGameState GetNetworkedGameStateRelativeTo(uint sequenceNumberRelativeTo)
    {
        var indexOfGameStateRelativeTo = cachedReceivedGameStates
            .FindIndex(ngs => ngs.SequenceNumber == sequenceNumberRelativeTo);

        Assert.IsTrue((indexOfGameStateRelativeTo >= 0) || (sequenceNumberRelativeTo == 0));

        return (indexOfGameStateRelativeTo >= 0)
            ? cachedReceivedGameStates[indexOfGameStateRelativeTo]
            : NetLib.GetEmptyNetworkedGameStateForDiffing();
    }
    
    public void ClientOnReceiveGameState(NetworkedGameState receivedGameState)
    {
        if (PlayerId == null) return;

        if (OsFps.Instance.IsRemoteClient)
        {
            Profiler.BeginSample("Client Get Current Networked Game State");
            var oldComponentLists = NetLib.GetComponentStateListsToSynchronize(
                receivedGameState.NetworkedComponentTypeInfos
            );
            Profiler.EndSample();

            Profiler.BeginSample("Client Apply Networked Game State");
            for (var i = 0; i < receivedGameState.NetworkedComponentStateLists.Count; i++)
            {
                var componentList = receivedGameState.NetworkedComponentStateLists[i];
                var networkedComponentTypeInfo = receivedGameState.NetworkedComponentTypeInfos[i];
                var componentType = networkedComponentTypeInfo.StateType;
                var oldComponentList = oldComponentLists[i];

                ApplyState(networkedComponentTypeInfo, oldComponentList, componentList);
            }
            Profiler.EndSample();
        }
    }

    [Rpc(ExecuteOn = NetworkLibrary.NetworkPeerType.Client)]
    public void ClientOnSetPlayerId(uint playerId)
    {
        PlayerId = playerId;
    }

    [Rpc(ExecuteOn = NetworkLibrary.NetworkPeerType.Client)]
    public void ClientOnTriggerPulled(uint playerId, Ray shotRay)
    {
        // Don't do anything if we pulled the trigger.
        if (playerId == PlayerId)
        {
            return;
        }

        var playerObjectComponent = PlayerObjectSystem.Instance.FindPlayerObjectComponent(playerId);
        ShowWeaponFireEffects(playerObjectComponent, shotRay);
    }

    [Rpc(ExecuteOn = NetworkLibrary.NetworkPeerType.Client)]
    public void ClientOnDetonateGrenade(uint id, float3 position, GrenadeType type)
    {
        ShowGrenadeExplosion(position, type);
    }

    [Rpc(ExecuteOn = NetworkLibrary.NetworkPeerType.Client)]
    public void ClientOnDetonateRocket(uint id, float3 position)
    {
        ShowRocketExplosion(position);
    }

    [Rpc(ExecuteOn = NetworkLibrary.NetworkPeerType.Client)]
    public void ClientOnReceiveChatMessage(uint? playerId, string message)
    {
        if (playerId.HasValue)
        {
            var playerName = PlayerObjectSystem.Instance.FindPlayerComponent(playerId.Value)?.State.Name;
            _chatMessages.Add(string.Format("{0}: {1}", playerName, message));
        }
        else
        {
            _chatMessages.Add(message);
        }
    }

    [Rpc(ExecuteOn = NetworkLibrary.NetworkPeerType.Client)]
    public void ClientOnChangeWeapon(uint playerId, byte weaponIndex)
    {
        if (playerId == PlayerId)
        {
            return;
        }

        var playerObjectComponent = PlayerObjectSystem.Instance.FindPlayerObjectComponent(playerId);
        if (playerObjectComponent == null) return;

        VisualEquipWeapon(playerObjectComponent.State);
    }
    #endregion

    public void InternalOnConnectedToServer()
    {
        ClientPeer.CallRpcOnServer("ServerOnReceivePlayerInfo", reliableSequencedChannelId, new
        {
            playerName = OsFps.Instance.Settings.PlayerName
        });
    }
    public void InternalOnDisconnectedFromServer()
    {
        OnDisconnectedFromServer?.Invoke();
    }

    private void SendPlayerInput()
    {
        if (PlayerId == null) return;

        var playerObjectComponent = PlayerObjectSystem.Instance.FindPlayerObjectComponent(PlayerId.Value);
        if (playerObjectComponent == null) return;

        ClientPeer.CallRpcOnServer("ServerOnReceivePlayerInput", unreliableStateUpdateChannelId, new
        {
            playerId = PlayerId.Value,
            playerInput = playerObjectComponent.State.Input,
            lookDirAngles = playerObjectComponent.State.LookDirAngles
        });
    }
}