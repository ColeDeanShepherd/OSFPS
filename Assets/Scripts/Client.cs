using NetLib;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Networking;

public class Client
{
    public ClientPeer ClientPeer;
    public uint? PlayerId;
    public GameObject Camera;
    public GameObject GuiContainer;
    public int ZoomLevel;

    public event ClientPeer.ServerConnectionChangeEventHandler OnDisconnectedFromServer;

    public GameStateScraperSystem gameStateScraperSystem = new GameStateScraperSystem();

    public void Start(bool isServerRemote)
    {
        ClientPeer = new ClientPeer();
        ClientPeer.OnReceiveDataFromServer += OnReceiveDataFromServer;
        ClientPeer.OnConnectedToServer += InternalOnConnectedToServer;
        ClientPeer.OnDisconnectedFromServer += InternalOnDisconnectedFromServer;

        var connectionConfig = OsFps.Instance.CreateConnectionConfig(
            out reliableSequencedChannelId,
            out reliableChannelId,
            out unreliableStateUpdateChannelId,
            out unreliableFragmentedChannelId,
            out unreliableChannelId
        );
        ClientPeer.Start(connectionConfig);

        SendInputPeriodicFunction = new ThrottledAction(SendPlayerInput, 1.0f / 30);

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

    public void Update()
    {
        Camera.GetComponent<Camera>().fieldOfView = GetCurrentFieldOfViewY();

        ClientPeer.ReceiveAndHandleNetworkEvents();

        if (ClientPeer.IsConnectedToServer)
        {
            if (PlayerId != null)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1))
                {
                    var playerObjectComponent = OsFps.Instance.FindPlayerObjectComponent(PlayerId.Value);

                    if (playerObjectComponent != null)
                    {
                        SwitchWeapons(playerObjectComponent, 0);
                    }
                }

                if (Input.GetKeyDown(KeyCode.Alpha2))
                {
                    var playerObjectComponent = OsFps.Instance.FindPlayerObjectComponent(PlayerId.Value);

                    if (playerObjectComponent != null)
                    {
                        SwitchWeapons(playerObjectComponent, 1);
                    }
                }

                var mouseScrollDirection = Input.GetAxis("Mouse ScrollWheel");
                if (mouseScrollDirection > 0)
                {
                    var playerObjectComponent = OsFps.Instance.FindPlayerObjectComponent(PlayerId.Value);
                    if (playerObjectComponent != null)
                    {
                        var newWeaponIndex = MathfExtensions.Wrap(
                            playerObjectComponent.State.CurrentWeaponIndex + 1,
                            0, playerObjectComponent.State.Weapons.Length - 1
                        );
                        SwitchWeapons(
                            playerObjectComponent,
                            newWeaponIndex
                        );
                    }
                }
                else if (mouseScrollDirection < 0)
                {
                    var playerObjectComponent = OsFps.Instance.FindPlayerObjectComponent(PlayerId.Value);
                    if (playerObjectComponent != null)
                    {
                        var newWeaponIndex = MathfExtensions.Wrap(
                            playerObjectComponent.State.CurrentWeaponIndex - 1,
                            0, playerObjectComponent.State.Weapons.Length - 1
                        );
                        SwitchWeapons(
                            playerObjectComponent,
                            newWeaponIndex
                        );
                    }
                }

                if (Input.GetKeyDown(OsFps.JumpKeyCode))
                {
                    var playerObjectComponent = OsFps.Instance.FindPlayerObjectComponent(PlayerId.Value);

                    if ((playerObjectComponent != null) && OsFps.Instance.IsPlayerGrounded(playerObjectComponent))
                    {
                        PlayerSystem.Instance.Jump(playerObjectComponent);
                        OsFps.Instance.CallRpcOnServer("ServerOnPlayerTryJump", reliableChannelId, new
                        {
                            playerId = PlayerId.Value
                        });
                    }
                }

                if (Input.GetKey(OsFps.PickupWeaponKeyCode))
                {
                    var playerObjectComponent = OsFps.Instance.FindPlayerObjectComponent(PlayerId.Value);

                    if (playerObjectComponent != null)
                    {
                        var playerId = playerObjectComponent.State.Id;
                        var playersClosestWeaponInfo = WeaponObjectSystem.Instance.ClosestWeaponInfoByPlayerId
                            .GetValueOrDefault(playerId);

                        if (playersClosestWeaponInfo != null)
                        {
                            var weaponId = playersClosestWeaponInfo.Item1;

                            OsFps.Instance.CallRpcOnServer("ServerOnPlayerTryPickupWeapon", reliableChannelId, new
                            {
                                playerId = playerId,
                                weaponId = weaponId
                            });
                        }
                    }
                }

                if (Input.GetKeyDown(OsFps.ZoomInKeyCode))
                {
                    ChangeZoomLevel();
                }
            }

            if (Input.GetKeyDown(OsFps.ChatKeyCode))
            {
                if (!_isShowingChatMessageInput)
                {
                    _isShowingChatMessageInput = true;
                    _justOpenedChatMessageInput = true;
                    _chatMessageBeingTyped = "";

                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
                else
                {
                    ConfirmChatMessage();
                }
            }

            if (Input.GetKeyDown(OsFps.ToggleMenuKeyCode))
            {
                if (!_isShowingMenu)
                {
                    var pauseScreenComponent = GameObject.Instantiate(
                        OsFps.Instance.PauseScreenPrefab, OsFps.Instance.CanvasObject.transform
                    ).GetComponent<PauseScreenComponent>();
                    OsFps.Instance.PushMenu(pauseScreenComponent);

                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
                else
                {
                    OsFps.Instance.PopMenu();
                }
            }

            if (!_isShowingChatMessageInput && !_isShowingMenu)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            SendInputPeriodicFunction.TryToCall();
        }
    }
    public void LateUpdate()
    {
        if (PlayerId != null)
        {
            var playerObjectComponent = OsFps.Instance.FindPlayerObjectComponent(PlayerId.Value);
            
            if (playerObjectComponent != null)
            {
                var playerObjectState = playerObjectComponent.State;
                playerObjectState.Position = playerObjectComponent.transform.position;
                playerObjectState.Velocity = playerObjectComponent.Rigidbody.velocity;
                playerObjectState.LookDirAngles = OsFps.Instance.GetPlayerLookDirAngles(playerObjectComponent);
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

        GameObject crosshair = Object.Instantiate(OsFps.Instance.CrosshairPrefab);
        crosshair.transform.SetParent(GuiContainer.transform);
        crosshair.transform.localPosition = Vector3.zero;
        crosshair.transform.localRotation = Quaternion.identity;
    }

    public void OnGui()
    {
        if (ClientPeer.IsConnectedToServer)
        {
            if (_isShowingConnectingScreen)
            {
                OsFps.Instance.PopMenu();
            }

            DrawHud();

            if (Input.GetKey(OsFps.ShowScoreboardKeyCode))
            {
                DrawScoreBoard();
            }

            DrawChatWindow();

            if (PlayerId.HasValue)
            {
                var closestWeaponInfo = WeaponObjectSystem.Instance.ClosestWeaponInfoByPlayerId
                    .GetValueOrDefault(PlayerId.Value);

                if (closestWeaponInfo != null)
                {
                    var weaponComponent = OsFps.Instance.FindWeaponComponent(closestWeaponInfo.Item1);
                    DrawWeaponPickupHud(weaponComponent);
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
                OsFps.Instance.PushMenu(connectingScreenComponent);
            }
        }
    }
    private void DrawHud()
    {
        const float hudMargin = 10;
        const float lineHeight = 20;
        const float weaponHudHeight = 5 + lineHeight;

        var playerObjectComponent = PlayerId.HasValue
            ? OsFps.Instance.FindPlayerObjectComponent(PlayerId.Value)
            : null;
        if (playerObjectComponent == null) return;

        var playerObjectState = playerObjectComponent.State;

        var shieldLabelRect = new Rect(hudMargin, hudMargin + lineHeight, 200, lineHeight);
        GUI.Label(shieldLabelRect, "Shield: " + Mathf.RoundToInt(playerObjectState.Shield));

        var healthLabelRect = new Rect(hudMargin, hudMargin, 200, lineHeight);
        GUI.Label(healthLabelRect, "Health: " + Mathf.RoundToInt(playerObjectState.Health));

        var weaponHudPosition = new Vector2(hudMargin + 110, hudMargin);

        if (playerObjectState.CurrentWeapon != null)
        {
            DrawWeaponHud(playerObjectState.CurrentWeapon, weaponHudPosition);
            weaponHudPosition.y += weaponHudHeight;
        }

        foreach (var weapon in playerObjectState.Weapons)
        {
            if ((weapon != null) && (weapon != playerObjectState.CurrentWeapon))
            {
                DrawWeaponHud(weapon, weaponHudPosition);
                weaponHudPosition.y += weaponHudHeight;
            }
        }

        var grenadeHudPosition = new Vector2(hudMargin + 280, hudMargin);
        if (playerObjectState.CurrentGrenadeSlot != null)
        {
            DrawGrenadeSlotHud(playerObjectState.CurrentGrenadeSlot, grenadeHudPosition);
            grenadeHudPosition.y += weaponHudHeight;
        }

        foreach (var grenadeSlot in playerObjectState.GrenadeSlots)
        {
            if ((grenadeSlot != null) && (grenadeSlot != playerObjectState.CurrentGrenadeSlot))
            {
                DrawGrenadeSlotHud(grenadeSlot, grenadeHudPosition);
                grenadeHudPosition.y += weaponHudHeight;
            }
        }
    }
    private void DrawWeaponHud(EquippedWeaponState weapon, Vector2 position)
    {
        GUI.Label(
            new Rect(position.x, position.y, 200, 50),
            weapon.Type.ToString() + " Ammo: " + weapon.BulletsLeftInMagazine + " / " + weapon.BulletsLeftOutOfMagazine
        );
    }
    private void DrawGrenadeSlotHud(GrenadeSlot grenadeSlot, Vector2 position)
    {
        GUI.Label(
            new Rect(position.x, position.y, 200, 50),
            grenadeSlot.GrenadeType.ToString() + " Grenades: " + grenadeSlot.GrenadeCount + " / " + OsFps.MaxGrenadesPerType
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
            var pingInSeconds = (playerState.Id == PlayerId) ? ClientPeer.RoundTripTime : null;
            var pingInMilliseconds = (pingInSeconds != null) ? (1000 * pingInSeconds) : null;
            var pingString = (pingInMilliseconds != null)
                ? Mathf.RoundToInt(pingInMilliseconds.Value).ToString()
                : "";

            GUI.Label(new Rect(idColumnX, position.y, playerIdColumnWidth, rowHeight), playerState.Name.ToString());
            GUI.Label(new Rect(pingColumnX, position.y, pingColumnWidth, rowHeight), pingString);
            GUI.Label(new Rect(killsColumnX, position.y, killsColumnWidth, rowHeight), playerState.Kills.ToString());
            GUI.Label(new Rect(deathsColumnX, position.y, deathsColumnWidth, rowHeight), playerState.Deaths.ToString());
            position.y += rowHeight;
        }
    }
    private void DrawChatWindow()
    {
        const float margin = 10;
        const float chatMessagesWidth = 300;
        const float chatMessagesHeight = 100;
        const float chatMessageInputWidth = chatMessagesWidth;
        const float chatMessageInputHeight = 30;
        const float totalWidth = chatMessagesWidth;
        const float totalHeight = chatMessagesHeight + chatMessageInputHeight;

        var chatMessagesRect = new Rect(
            new Vector2(margin, Screen.height - totalHeight - margin),
            new Vector2(chatMessagesWidth, chatMessagesHeight)
        );

        if (_isShowingChatMessageInput)
        {
            chatMessageScrollPosition = GUI.BeginScrollView(
                chatMessagesRect,
                chatMessageScrollPosition,
                new Rect(0, 0, chatMessagesWidth, 400)
            );

            GUI.Label(new Rect(0, 0, chatMessagesWidth, chatMessagesHeight), string.Join("\n", _chatMessages));

            GUI.EndScrollView();
        }
        else
        {
            GUI.Label(chatMessagesRect, string.Join("\n", _chatMessages));
        }
        
        if (_isShowingChatMessageInput)
        {
            if ((Event.current.type == EventType.KeyDown) && (Event.current.keyCode == KeyCode.Return))
            {
                ConfirmChatMessage();
            }

            GUI.SetNextControlName("chatMessageInput");
            var chatMessageTextFieldRect = new Rect(
                new Vector3(margin, Screen.height - chatMessageInputHeight - margin),
                new Vector2(chatMessageInputWidth, chatMessageInputHeight)
            );
            _chatMessageBeingTyped = GUI.TextField(chatMessageTextFieldRect, _chatMessageBeingTyped);
        }

        if (_justOpenedChatMessageInput)
        {
            GUI.FocusControl("chatMessageInput");
            _justOpenedChatMessageInput = false;
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

    public bool _isShowingChatMessageInput;
    public bool _justOpenedChatMessageInput;
    private string _chatMessageBeingTyped;
    public List<string> _chatMessages = new List<string>();
    private Vector2 chatMessageScrollPosition = new Vector2();

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
        //if (ZoomLevel == 0) return true;
        if (ZoomLevel == 2) return false;

        if (PlayerId == null) return false;

        var playerObjectComponent = OsFps.Instance.FindPlayerObjectComponent(PlayerId.Value);
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
    private void AttachCameraToPlayer(uint playerId)
    {
        var playerObject = OsFps.Instance.FindPlayerObject(playerId);
        var cameraPointObject = playerObject.GetComponent<PlayerObjectComponent>().CameraPointObject;
        
        Camera.transform.SetParent(cameraPointObject.transform);

        Camera.transform.localPosition = Vector3.zero;
        Camera.transform.localRotation = Quaternion.identity;
    }
    private void DetachCameraFromPlayer()
    {
        Camera.transform.SetParent(null, true);
    }
    private EquippedWeaponComponent GetEquippedWeaponComponent(PlayerObjectComponent playerObjectComponent)
    {
        foreach (Transform weaponTransform in playerObjectComponent.HandsPointObject.transform)
        {
            var equippedWeaponComponent =  weaponTransform.gameObject.GetComponent<EquippedWeaponComponent>();
            if (equippedWeaponComponent != null)
            {
                return equippedWeaponComponent;
            }
        }

        return null;
    }
    private void VisualEquipWeapon(PlayerObjectState playerObjectState)
    {
        var playerObjectComponent = OsFps.Instance.FindPlayerObjectComponent(playerObjectState.Id);

        var equippedWeaponComponent = GetEquippedWeaponComponent(playerObjectComponent);
        var wasEQCNull = equippedWeaponComponent == null;
        if (equippedWeaponComponent != null)
        {
            Object.DestroyImmediate(equippedWeaponComponent.gameObject);
        }

        if (playerObjectState.CurrentWeapon != null)
        {
            var weaponPrefab = OsFps.Instance.GetWeaponDefinitionByType(playerObjectState.CurrentWeapon.Type).Prefab;
            GameObject weaponObject = Object.Instantiate(weaponPrefab, Vector3.zero, Quaternion.identity);
            weaponObject.transform.SetParent(playerObjectComponent.HandsPointObject.transform, false);

            var weaponComponent = weaponObject.GetComponent<WeaponComponent>();
            Object.DestroyImmediate(weaponComponent.Rigidbody);
            Object.DestroyImmediate(weaponComponent.Collider);
            Object.DestroyImmediate(weaponComponent);

            equippedWeaponComponent = weaponObject.AddComponent<EquippedWeaponComponent>();
            equippedWeaponComponent.State = playerObjectState.CurrentWeapon;
            equippedWeaponComponent.State.TimeSinceLastShot = equippedWeaponComponent.State.Definition.ShotInterval;

            playerObjectState.CurrentWeapon.TimeSinceLastShot = playerObjectState.CurrentWeapon.Definition.ShotInterval;
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
        OsFps.Instance.CallRpcOnServer("ServerOnPlayerReloadPressed", reliableChannelId, new
        {
            playerId = playerObjectComponent.State.Id
        });

        var equippedWeaponComponent = GetEquippedWeaponComponent(playerObjectComponent);
        var audioSource = equippedWeaponComponent?.GetComponent<AudioSource>();
        audioSource?.PlayOneShot(OsFps.Instance.ReloadSound);
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
    public void ShowWeaponFireEffects(PlayerObjectComponent playerObjectComponent, Ray shotRay)
    {
        ShowMuzzleFlash(playerObjectComponent);

        var weapon = playerObjectComponent.State.CurrentWeapon;
        if (weapon != null)
        {
            if (weapon.Type == WeaponType.SniperRifle)
            {
                OsFps.Instance.CreateSniperBulletTrail(shotRay);
            }

            var equippedWeaponComponent = GetEquippedWeaponComponent(playerObjectComponent);
            if (equippedWeaponComponent != null)
            {
                var weaponAudioSource = equippedWeaponComponent.GetComponent<AudioSource>();
                weaponAudioSource?.PlayOneShot(weapon.Definition.ShotSound);
            }

            if (weapon.Definition.IsHitScan)
            {
                CreateBulletHole(shotRay);
            }
        }
    }
    private void CreateBulletHole(Ray shotRay)
    {
        RaycastHit raycastHit;
        if (Physics.Raycast(shotRay, out raycastHit))
        {
            var bulletHolePosition = raycastHit.point + (0.01f * raycastHit.normal);
            var bulletHoleOrientation = Quaternion.LookRotation(-raycastHit.normal);
            var bulletHole = GameObject.Instantiate(
                OsFps.Instance.BulletHolePrefab, bulletHolePosition, bulletHoleOrientation, raycastHit.transform
            );
            Object.Destroy(bulletHole, 5);
        }
    }
    public void ShowGrenadeExplosion(Vector3 position, GrenadeType grenadeType)
    {
        var explosionPrefab = OsFps.Instance.GetGrenadeDefinitionByType(grenadeType).ExplosionPrefab;
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
    public void PlayerShoot(PlayerObjectComponent playerObjectComponent)
    {
        OsFps.Instance.CallRpcOnServer("ServerOnPlayerTriggerPulled", reliableChannelId, new
        {
            playerId = playerObjectComponent.State.Id,
            shotRay = PlayerSystem.Instance.GetShotRay(playerObjectComponent)
        });

        // predict the shot
        ShowWeaponFireEffects(playerObjectComponent, PlayerSystem.Instance.GetShotRay(playerObjectComponent));

        playerObjectComponent.State.CurrentWeapon.TimeSinceLastShot = 0;
    }

    public void ThrowGrenade(PlayerObjectState playerObjectState)
    {
        OsFps.Instance.CallRpcOnServer("ServerOnPlayerThrowGrenade", reliableChannelId, new
        {
            playerId = playerObjectState.Id
        });

        playerObjectState.TimeUntilCanThrowGrenade = OsFps.GrenadeThrowInterval;
    }
    public void SwitchGrenadeType(PlayerObjectState playerObjectState)
    {
        OsFps.Instance.CallRpcOnServer("ServerOnPlayerSwitchGrenadeType", reliableChannelId, new {
            playerId = playerObjectState.Id
        });
    }

    public void SwitchWeapons(PlayerObjectComponent playerObjectComponent, int weaponIndex)
    {
        Assert.IsNotNull(playerObjectComponent);

        var playerObjectState = playerObjectComponent.State;

        if (weaponIndex == playerObjectState.CurrentWeaponIndex) return;

        // Send message to server.
        OsFps.Instance.CallRpcOnServer("ServerOnChangeWeapon", reliableSequencedChannelId, new
        {
            playerId = playerObjectState.Id,
            weaponIndex = (byte)weaponIndex
        });
    }

    private void ConfirmChatMessage()
    {
        if (_chatMessageBeingTyped.Length > 0)
        {
            OsFps.Instance.CallRpcOnServer("ServerOnChatMessage", reliableChannelId, new
            {
                playerId = PlayerId,
                message = _chatMessageBeingTyped
            });

            _chatMessageBeingTyped = "";
        }

        _isShowingChatMessageInput = false;
    }

    private Vector3 CorrectedPosition(Vector3 serverPosition, Vector3 serverVelocity, float roundTripTime, Vector3 clientPosition)
    {
        var serverToClientLatency = roundTripTime / 2;
        var predictedPosition = serverPosition + (serverToClientLatency * serverVelocity);
        var positionDifference = predictedPosition - clientPosition;
        var percentOfDiffToCorrect = 1f / 3;
        var positionDelta = percentOfDiffToCorrect * positionDifference;
        return clientPosition + positionDelta;
    }
    private Vector3 CorrectedEulerAngles(Vector3 serverEulerAngles, Vector3 serverAngularVelocity, float roundTripTime, Vector3 clientEulerAngles)
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
    private Vector3 CorrectedVelocity(Vector3 serverVelocity, float roundTripTime, Vector3 clientVelocity)
    {
        var serverToClientLatency = roundTripTime / 2;
        var percentOfDiffToCorrect = 1f / 2;
        var velocityDiff = percentOfDiffToCorrect * (serverVelocity - clientVelocity);
        return clientVelocity + velocityDiff;
    }
    private Vector3 CorrectedAngularVelocity(Vector3 serverAngularVelocity, float roundTripTime, Vector3 clientAngularVelocity)
    {
        var serverToClientLatency = roundTripTime / 2;
        var percentOfDiffToCorrect = 1f / 2;
        var angularVelocityDiff = percentOfDiffToCorrect * (serverAngularVelocity - clientAngularVelocity);
        return clientAngularVelocity + angularVelocityDiff;
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

    private void ApplyRigidbodyState(RigidBodyState newRigidBodyState, RigidBodyState oldRigidBodyState, Rigidbody rigidbody)
    {
        var roundTripTime = ClientPeer.RoundTripTime.Value;

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

    public void ApplyGameState(GameState newGameState)
    {
        if (!PlayerId.HasValue) return; // Wait until the player ID has been set.

        var oldGameState = gameStateScraperSystem.GetGameState();

        ApplyPlayerStates(oldGameState, newGameState);
        ApplyPlayerObjectStates(oldGameState, newGameState);
        ApplyWeaponObjectStates(oldGameState, newGameState);
        //ApplyWeaponSpawnerStates(oldGameState, newGameState);
        ApplyGrenadeStates(oldGameState, newGameState);
        ApplyRocketStates(oldGameState, newGameState);
    }

    private void ApplyPlayerStates(GameState oldGameState, GameState newGameState)
    {
        System.Func<PlayerState, PlayerState, bool> doIdsMatch =
            (ps1, ps2) => ps1.Id == ps2.Id;

        System.Action<PlayerState> handleRemovedPlayerState = removedPlayerState =>
        {
            var playerComponent = OsFps.Instance.FindPlayerComponent(removedPlayerState.Id);
            Object.Destroy(playerComponent);
        };

        System.Action<PlayerState> handleAddedPlayerState = addedPlayerState =>
            OsFps.Instance.CreateLocalPlayerDataObject(addedPlayerState);

        System.Action<PlayerState, PlayerState> handleUpdatedPlayerState =
            (oldPlayerState, updatedPlayerState) =>
                ApplyPlayerState(oldGameState, updatedPlayerState);

        ApplyStates(
            oldGameState.Players, newGameState.Players, doIdsMatch,
            handleRemovedPlayerState, handleAddedPlayerState, handleUpdatedPlayerState
        );
    }
    private void ApplyPlayerState(GameState oldGameState, PlayerState updatedPlayerState)
    {
        var playerComponent = OsFps.Instance.FindPlayerComponent(updatedPlayerState.Id);
        playerComponent.State = updatedPlayerState;
    }

    private void ApplyPlayerObjectStates(GameState oldGameState, GameState newGameState)
    {
        System.Func<PlayerObjectState, PlayerObjectState, bool> doIdsMatch =
            (pos1, pos2) => pos1.Id == pos2.Id;

        System.Action<PlayerObjectState> handleRemovedPlayerObjectState = removedPlayerObjectState =>
        {
            if (removedPlayerObjectState.Id == PlayerId)
            {
                DetachCameraFromPlayer();
            }

            Object.Destroy(OsFps.Instance.FindPlayerObject(removedPlayerObjectState.Id));
        };

        System.Action<PlayerObjectState> handleAddedPlayerObjectState = addedPlayerObjectState =>
            SpawnPlayer(addedPlayerObjectState);

        System.Action<PlayerObjectState, PlayerObjectState> handleUpdatedPlayerObjectState =
            (oldPlayerObjectState, updatedPlayerObjectState) =>
                ApplyPlayerObjectState(oldGameState, updatedPlayerObjectState);

        ApplyStates(
            oldGameState.PlayerObjects, newGameState.PlayerObjects, doIdsMatch,
            handleRemovedPlayerObjectState, handleAddedPlayerObjectState, handleUpdatedPlayerObjectState
        );
    }
    private void ApplyPlayerObjectState(GameState oldGameState, PlayerObjectState updatedPlayerObjectState)
    {
        var currentPlayerObjectStateIndex = oldGameState.PlayerObjects
                .FindIndex(curPs => curPs.Id == updatedPlayerObjectState.Id);
        var currentPlayerObjectState = oldGameState.PlayerObjects[currentPlayerObjectStateIndex];

        var isPlayerMe = updatedPlayerObjectState.Id == PlayerId;
        var roundTripTime = ClientPeer.RoundTripTime.Value;

        var playerObject = OsFps.Instance.FindPlayerObject(updatedPlayerObjectState.Id);

        // Handle weapon pickup.
        if (playerObject != null)
        {
            var playerObjectComponent = playerObject.GetComponent<PlayerObjectComponent>();
            var equippedWeaponType = GetEquippedWeaponComponent(playerObjectComponent)?.State.Type;
            var newWeaponType = updatedPlayerObjectState.CurrentWeapon?.Type;

            if (newWeaponType != equippedWeaponType)
            {
                VisualEquipWeapon(updatedPlayerObjectState);
            }
        }

        // Update player object.
        if (playerObject != null)
        {
            var playerObjectComponent = playerObject.GetComponent<PlayerObjectComponent>();

            // Correct position.
            var correctedPosition = CorrectedPosition(
                updatedPlayerObjectState.Position, updatedPlayerObjectState.Velocity,
                roundTripTime, playerObjectComponent.transform.position
            );
            playerObjectComponent.transform.position = correctedPosition;
            updatedPlayerObjectState.Position = correctedPosition;

            // Correct velocity.
            var correctedVelocity = CorrectedVelocity(
                updatedPlayerObjectState.Velocity, roundTripTime, playerObjectComponent.Rigidbody.velocity
            );
            playerObjectComponent.Rigidbody.velocity = correctedVelocity;
            updatedPlayerObjectState.Velocity = correctedVelocity;

            // Update look direction.
            if (isPlayerMe)
            {
                updatedPlayerObjectState.LookDirAngles = OsFps.Instance.GetPlayerLookDirAngles(playerObjectComponent);
            }

            OsFps.Instance.ApplyLookDirAnglesToPlayer(playerObjectComponent, updatedPlayerObjectState.LookDirAngles);

            // Update weapon if reloading.
            var equippedWeaponComponent = GetEquippedWeaponComponent(playerObjectComponent);
            if ((equippedWeaponComponent != null) && updatedPlayerObjectState.IsReloading)
            {
                var percentDoneReloading = updatedPlayerObjectState.ReloadTimeLeft / updatedPlayerObjectState.CurrentWeapon.Definition.ReloadTime;

                var y = -(1.0f - Mathf.Abs((2 * percentDoneReloading) - 1));
                var z = equippedWeaponComponent.transform.localPosition.z;
                equippedWeaponComponent.transform.localPosition = new Vector3(0, y, z);
            }

            // Update weapon recoil.
            if ((equippedWeaponComponent != null) && (updatedPlayerObjectState.CurrentWeapon != null))
            {
                var percentDoneWithRecoil = Mathf.Min(
                    updatedPlayerObjectState.CurrentWeapon.TimeSinceLastShot /
                    updatedPlayerObjectState.CurrentWeapon.Definition.RecoilTime,
                    1
                );
                var y = equippedWeaponComponent.transform.localPosition.y;
                var z = Mathf.Lerp(-0.1f, 0, percentDoneWithRecoil);
                equippedWeaponComponent.transform.localPosition = new Vector3(0, y, z);
            }

            // Update shields.
            var shieldAlpha = 1.0f - (playerObjectComponent.State.Shield / OsFps.MaxPlayerShield);
            OsFps.Instance.SetShieldAlpha(playerObjectComponent, shieldAlpha);
        }

        // Update state.
        if (isPlayerMe)
        {
            if ((updatedPlayerObjectState.CurrentWeapon != null) && (currentPlayerObjectState.CurrentWeapon != null))
            {
                updatedPlayerObjectState.CurrentWeapon.TimeSinceLastShot =
                    currentPlayerObjectState.CurrentWeapon.TimeSinceLastShot;
            }

            updatedPlayerObjectState.TimeUntilCanThrowGrenade = currentPlayerObjectState.TimeUntilCanThrowGrenade;

            updatedPlayerObjectState.Input = currentPlayerObjectState.Input;
        }

        if (playerObject != null)
        {
            var playerComponent = playerObject.GetComponent<PlayerObjectComponent>();
            playerComponent.State = updatedPlayerObjectState;
        }
    }

    private void ApplyWeaponObjectStates(GameState oldGameState, GameState newGameState)
    {
        System.Func<WeaponObjectState, WeaponObjectState, bool> doIdsMatch =
            (wos1, wos2) => wos1.Id == wos2.Id;

        System.Action<WeaponObjectState> handleRemovedWeaponObjectState = removedWeaponObjectState => 
            Object.Destroy(OsFps.Instance.FindWeaponObject(removedWeaponObjectState.Id));

        System.Action<WeaponObjectState> handleAddedWeaponObjectState = addedWeaponObjectState =>
            OsFps.Instance.SpawnLocalWeaponObject(addedWeaponObjectState);

        System.Action<WeaponObjectState, WeaponObjectState> handleUpdatedWeaponObjectState =
            (oldWeaponObjectState, updatedWeaponObjectState) => ApplyWeaponObjectState(updatedWeaponObjectState);

        ApplyStates(
            oldGameState.WeaponObjects, newGameState.WeaponObjects, doIdsMatch,
            handleRemovedWeaponObjectState, handleAddedWeaponObjectState, handleUpdatedWeaponObjectState
        );
    }
    private void ApplyWeaponObjectState(WeaponObjectState updatedWeaponObjectState)
    {
        var weaponComponent = OsFps.Instance.FindWeaponComponent(updatedWeaponObjectState.Id);
        var currentWeaponObjectState = weaponComponent.State;

        // Update weapon object.
        if (weaponComponent != null)
        {
            ApplyRigidbodyState(
                updatedWeaponObjectState.RigidBodyState,
                currentWeaponObjectState.RigidBodyState,
                weaponComponent.Rigidbody
            );

            // Update state.
            weaponComponent.State = updatedWeaponObjectState;
        }
    }

    private void ApplyGrenadeStates(GameState oldGameState, GameState newGameState)
    {
        System.Func<GrenadeState, GrenadeState, bool> doIdsMatch =
            (g1, g2) => g1.Id == g2.Id;

        System.Action<GrenadeState> handleRemovedGrenadeState = removedGrenadeState =>
        {
            var grenadeComponent = OsFps.Instance.FindGrenadeComponent(removedGrenadeState.Id);
            Object.Destroy(grenadeComponent.gameObject);
        };

        System.Action<GrenadeState> handleAddedGrenadeState = addedGrenadeState =>
            OsFps.Instance.SpawnLocalGrenadeObject(addedGrenadeState);

        System.Action<GrenadeState, GrenadeState> handleUpdatedGrenadeState =
            (oldGrenadeState, updatedGrenadeState) => ApplyGrenadeState(updatedGrenadeState);

        ApplyStates(
            oldGameState.Grenades, newGameState.Grenades, doIdsMatch,
            handleRemovedGrenadeState, handleAddedGrenadeState, handleUpdatedGrenadeState
        );
    }
    private void ApplyGrenadeState(GrenadeState updatedGrenadeState)
    {
        var grenadeComponent = OsFps.Instance.FindGrenadeComponent(updatedGrenadeState.Id);
        var currentGrenadeState = grenadeComponent.State;

        // Update weapon object.
        if (grenadeComponent != null)
        {
            ApplyRigidbodyState(
                updatedGrenadeState.RigidBodyState,
                currentGrenadeState.RigidBodyState,
                grenadeComponent.Rigidbody
            );

            // Update state.
            grenadeComponent.State = updatedGrenadeState;
        }
    }

    private void ApplyRocketStates(GameState oldGameState, GameState newGameState)
    {
        System.Func<RocketState, RocketState, bool> doIdsMatch =
            (rs1, rs2) => rs1.Id == rs2.Id;

        System.Action<RocketState> handleRemovedRocketState = removedRocketState =>
        {
            var rocketComponent = OsFps.Instance.FindRocketComponent(removedRocketState.Id);
            Object.Destroy(rocketComponent.gameObject);
        };

        System.Action<RocketState> handleAddedRocketState = addedRocketState =>
            OsFps.Instance.SpawnLocalRocketObject(addedRocketState);

        System.Action<RocketState, RocketState> handleUpdatedRocketState =
            (oldRocketState, updatedRocketState) => ApplyRocketState(updatedRocketState);

        ApplyStates(
            oldGameState.Rockets, newGameState.Rockets, doIdsMatch,
            handleRemovedRocketState, handleAddedRocketState, handleUpdatedRocketState
        );
    }
    private void ApplyRocketState(RocketState updatedRocketState)
    {
        var rocketComponent = OsFps.Instance.FindRocketComponent(updatedRocketState.Id);
        var currentRocketState = rocketComponent.State;

        // Update weapon object.
        if (rocketComponent != null)
        {
            ApplyRigidbodyState(
                updatedRocketState.RigidBodyState,
                currentRocketState.RigidBodyState,
                rocketComponent.Rigidbody
            );

            // Update state.
            rocketComponent.State = updatedRocketState;
        }
    }

    private void SpawnPlayer(PlayerObjectState playerState)
    {
        OsFps.Instance.SpawnLocalPlayer(playerState);
        //VisualEquipWeapon(playerState);

        if (playerState.Id == PlayerId)
        {
            AttachCameraToPlayer(playerState.Id);
        }
    }

    #region Message Handlers
    private void OnReceiveDataFromServer(int channelId, byte[] bytesReceived)
    {
        var reader = new BinaryReader(new MemoryStream(bytesReceived));
        var messageTypeAsByte = reader.ReadByte();

        RpcInfo rpcInfo;

        if (OsFps.Instance.rpcInfoById.TryGetValue(messageTypeAsByte, out rpcInfo))
        {
            var rpcArguments = NetworkSerializationUtils.DeserializeRpcCallArguments(rpcInfo, reader);
            OsFps.Instance.ExecuteRpc(rpcInfo.Id, rpcArguments);
        }
        else
        {
            throw new System.NotImplementedException("Unknown message type: " + messageTypeAsByte);
        }
    }

    [Rpc(ExecuteOn = NetworkPeerType.Client)]
    public void ClientOnSetPlayerId(uint playerId)
    {
        PlayerId = playerId;
    }

    [Rpc(ExecuteOn = NetworkPeerType.Client)]
    public void ClientOnReceiveGameState(GameState gameState)
    {
        ApplyGameState(gameState);
    }

    [Rpc(ExecuteOn = NetworkPeerType.Client)]
    public void ClientOnTriggerPulled(uint playerId, Ray shotRay)
    {
        // Don't do anything if we pulled the trigger.
        if (playerId == PlayerId)
        {
            return;
        }

        var playerObjectComponent = OsFps.Instance.FindPlayerObjectComponent(playerId);
        ShowWeaponFireEffects(playerObjectComponent, shotRay);
    }

    [Rpc(ExecuteOn = NetworkPeerType.Client)]
    public void ClientOnDetonateGrenade(uint id, Vector3 position, GrenadeType type)
    {
        ShowGrenadeExplosion(position, type);
    }

    [Rpc(ExecuteOn = NetworkPeerType.Client)]
    public void ClientOnDetonateRocket(uint id, Vector3 position)
    {
        ShowRocketExplosion(position);
    }

    [Rpc(ExecuteOn = NetworkPeerType.Client)]
    public void ClientOnReceiveChatMessage(uint? playerId, string message)
    {
        if (playerId.HasValue)
        {
            var playerName = OsFps.Instance.FindPlayerComponent(playerId.Value)?.State.Name;
            _chatMessages.Add(string.Format("{0}: {1}", playerId, message));
        }
        else
        {
            _chatMessages.Add(message);
        }
    }

    [Rpc(ExecuteOn = NetworkPeerType.Client)]
    public void ClientOnChangeWeapon(uint playerId, byte weaponIndex)
    {
        if (playerId == PlayerId)
        {
            return;
        }

        var playerObjectComponent = OsFps.Instance.FindPlayerObjectComponent(playerId);
        if (playerObjectComponent == null) return;

        SwitchWeapons(playerObjectComponent, weaponIndex);
    }
    #endregion

    public void InternalOnConnectedToServer()
    {
        OsFps.Instance.CallRpcOnServer("ServerOnReceivePlayerInfo", reliableSequencedChannelId, new
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

        var playerObjectComponent = OsFps.Instance.FindPlayerObjectComponent(PlayerId.Value);
        if (playerObjectComponent == null) return;

        OsFps.Instance.CallRpcOnServer("ServerOnReceivePlayerInput", unreliableStateUpdateChannelId, new
        {
            playerId = PlayerId.Value,
            playerInput = playerObjectComponent.State.Input,
            lookDirAngles = playerObjectComponent.State.LookDirAngles
        });
    }
}