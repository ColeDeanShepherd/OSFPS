using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Networking;
using NetworkLibrary;
using UnityEngine.Profiling;

public class Client
{
    public ClientPeer ClientPeer;
    public uint? PlayerId;
    public GameObject Camera;
    public GameObject GuiContainer;
    public ChatBoxComponent ChatBox;
    public int ZoomLevel;

    public event ClientPeer.ServerConnectionChangeEventHandler OnDisconnectedFromServer;

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

        Profiler.BeginSample("ReceiveAndHandleNetworkEvents");
        ClientPeer.ReceiveAndHandleNetworkEvents();
        Profiler.EndSample();

        if (ClientPeer.IsConnectedToServer)
        {
            if (PlayerId != null)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1))
                {
                    var playerObjectComponent = PlayerSystem.Instance.FindPlayerObjectComponent(PlayerId.Value);

                    if (playerObjectComponent != null)
                    {
                        SwitchWeapons(playerObjectComponent, 0);
                    }
                }

                if (Input.GetKeyDown(KeyCode.Alpha2))
                {
                    var playerObjectComponent = PlayerSystem.Instance.FindPlayerObjectComponent(PlayerId.Value);

                    if (playerObjectComponent != null)
                    {
                        SwitchWeapons(playerObjectComponent, 1);
                    }
                }

                var mouseScrollDirection = Input.GetAxis("Mouse ScrollWheel");
                if (mouseScrollDirection > 0)
                {
                    var playerObjectComponent = PlayerSystem.Instance.FindPlayerObjectComponent(PlayerId.Value);
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
                    var playerObjectComponent = PlayerSystem.Instance.FindPlayerObjectComponent(PlayerId.Value);
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

                if (Input.GetButtonDown("Jump"))
                {
                    var playerObjectComponent = PlayerSystem.Instance.FindPlayerObjectComponent(PlayerId.Value);

                    if ((playerObjectComponent != null) && PlayerSystem.Instance.IsPlayerGrounded(playerObjectComponent))
                    {
                        PlayerSystem.Instance.Jump(playerObjectComponent);
                        ClientPeer.CallRpcOnServer("ServerOnPlayerTryJump", reliableChannelId, new
                        {
                            playerId = PlayerId.Value
                        });
                    }
                }

                if (Input.GetButton("Pickup Weapon"))
                {
                    var playerObjectComponent = PlayerSystem.Instance.FindPlayerObjectComponent(PlayerId.Value);

                    if (playerObjectComponent != null)
                    {
                        var playerId = playerObjectComponent.State.Id;
                        var playersClosestWeaponInfo = WeaponObjectSystem.Instance.ClosestWeaponInfoByPlayerId
                            .GetValueOrDefault(playerId);

                        if (playersClosestWeaponInfo != null)
                        {
                            var weaponId = playersClosestWeaponInfo.Item1;

                            ClientPeer.CallRpcOnServer("ServerOnPlayerTryPickupWeapon", reliableChannelId, new
                            {
                                playerId = playerId,
                                weaponId = weaponId
                            });
                        }
                    }
                }

                if (Input.GetButtonDown("Zoom"))
                {
                    ChangeZoomLevel();
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

            var chatMessagesText = string.Join("\n", _chatMessages);
            ChatBox.VisualMessagesText.text = chatMessagesText;
            ChatBox.ScrollableMessagesText.text = chatMessagesText;

            SendInputPeriodicFunction.TryToCall();

            if (PlayerId != null)
            {
                if (!OsFps.Instance.IsRemoteClient && (Camera.transform.parent == null))
                {
                    var playerObjectComponent = PlayerSystem.Instance.FindPlayerObjectComponent(PlayerId.Value);
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
            var playerObjectComponent = PlayerSystem.Instance.FindPlayerObjectComponent(PlayerId.Value);
            
            if (playerObjectComponent != null)
            {
                var playerObjectState = playerObjectComponent.State;
                playerObjectState.Position = playerObjectComponent.transform.position;
                playerObjectState.Velocity = playerObjectComponent.Rigidbody.velocity;
                playerObjectState.LookDirAngles = PlayerSystem.Instance.GetPlayerLookDirAngles(playerObjectComponent);
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
                OsFps.Instance.PopMenu();
            }

            DrawHud();

            if (Input.GetButton("Show Scoreboard"))
            {
                DrawScoreBoard();
            }

            if (PlayerId.HasValue)
            {
                var closestWeaponInfo = WeaponObjectSystem.Instance.ClosestWeaponInfoByPlayerId
                    .GetValueOrDefault(PlayerId.Value);

                if (closestWeaponInfo != null)
                {
                    var weaponComponent = WeaponSystem.Instance.FindWeaponComponent(closestWeaponInfo.Item1);
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
            ? PlayerSystem.Instance.FindPlayerObjectComponent(PlayerId.Value)
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
        //if (ZoomLevel == 0) return true;
        if (ZoomLevel == 2) return false;

        if (PlayerId == null) return false;

        var playerObjectComponent = PlayerSystem.Instance.FindPlayerObjectComponent(PlayerId.Value);
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
        var playerObject = PlayerSystem.Instance.FindPlayerObject(playerId);
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
            var equippedWeaponComponent =  weaponTransform.gameObject.GetComponent<EquippedWeaponComponent>();
            if (equippedWeaponComponent != null)
            {
                return equippedWeaponComponent;
            }
        }

        return null;
    }
    public void VisualEquipWeapon(PlayerObjectState playerObjectState)
    {
        var playerObjectComponent = PlayerSystem.Instance.FindPlayerObjectComponent(playerObjectState.Id);

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
        ClientPeer.CallRpcOnServer("ServerOnPlayerReloadPressed", reliableChannelId, new
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
                WeaponSystem.Instance.CreateSniperBulletTrail(shotRay);
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
    public void PlayerShoot(PlayerObjectComponent playerObjectComponent)
    {
        ClientPeer.CallRpcOnServer("ServerOnPlayerTriggerPulled", reliableChannelId, new
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

    public void SwitchWeapons(PlayerObjectComponent playerObjectComponent, int weaponIndex)
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
            return PlayerSystem.Instance.CreateLocalPlayerDataObject((PlayerState)state);
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
        NetworkSynchronizedComponentInfo synchronizedComponentInfo, List<object> oldStates, List<object> newStates
    )
    {
        System.Func<object, object, bool> doIdsMatch =
            (s1, s2) => NetLib.GetIdFromState(synchronizedComponentInfo, s1) == NetLib.GetIdFromState(synchronizedComponentInfo, s2);

        System.Action<object> handleRemovedState = removedState =>
        {
            var monoBehaviour = NetLib.GetMonoBehaviourByState(synchronizedComponentInfo, removedState);
            Object.Destroy(monoBehaviour.gameObject);
        };

        System.Action<object> handleAddedState = addedState =>
        {
            var gameObject = CreateGameObjectFromState(addedState);
            
            if (
                (synchronizedComponentInfo != null) &&
                (synchronizedComponentInfo.MonoBehaviourApplyStateMethod != null)
            )
            {
                var stateId = NetLib.GetIdFromState(synchronizedComponentInfo, addedState);
                var monoBehaviour = NetLib.GetMonoBehaviourByStateId(synchronizedComponentInfo, stateId);
                
                synchronizedComponentInfo.MonoBehaviourApplyStateMethod.Invoke(monoBehaviour, new[] { addedState });
            }
        };

        System.Action<object, object> handleUpdatedState =
            (oldState, newState) =>
            {
                var oldStateId = NetLib.GetIdFromState(synchronizedComponentInfo, oldState);
                var monoBehaviour = NetLib.GetMonoBehaviourByStateId(synchronizedComponentInfo, oldStateId);

                synchronizedComponentInfo.MonoBehaviourStateField.SetValue(monoBehaviour, newState);
                synchronizedComponentInfo.MonoBehaviourApplyStateMethod?.Invoke(monoBehaviour, new[] { newState });
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
        }

        return playerObject;
    }

    #region Message Handlers
    private uint latestStateSequenceNumber = 0;
    private void OnReceiveDataFromServer(int channelId, byte[] bytesReceived, int numBytesReceived)
    {
        Profiler.BeginSample("OnReceiveDataFromServer");
        using (var memoryStream = new MemoryStream(bytesReceived, 0, numBytesReceived))
        {
            using (var reader = new BinaryReader(memoryStream))
            {
                var messageTypeAsByte = reader.ReadByte();

                RpcInfo rpcInfo;
                
                if (messageTypeAsByte == OsFps.StateSynchronizationMessageId)
                {
                    var sequenceNumber = reader.ReadUInt32();

                    if (sequenceNumber > latestStateSequenceNumber)
                    {
                        Profiler.BeginSample("State Deserialization");
                        var componentLists = NetworkSerializationUtils.DeserializeSynchronizedComponents(
                            reader, NetLib.synchronizedComponentInfos
                        );
                        Profiler.EndSample();

                        Profiler.BeginSample("ClientOnReceiveGameState");
                        ClientOnReceiveGameState(sequenceNumber, componentLists);
                        Profiler.EndSample();
                    }
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

    public void ClientOnReceiveGameState(uint sequenceNumber, List<List<object>> componentLists)
    {
        if (PlayerId == null) return;

        ClientPeer.CallRpcOnServer("ServerOnReceiveClientGameStateAck", unreliableChannelId, new
        {
            playerId = PlayerId.Value,
            gameStateSequenceNumber = sequenceNumber
        });

        latestStateSequenceNumber = sequenceNumber;

        if (OsFps.Instance.IsRemoteClient)
        {
            Profiler.BeginSample("NetLib.GetStateObjectListsToSynchronize");
            var oldComponentLists = NetLib.GetStateObjectListsToSynchronize(
                NetLib.synchronizedComponentInfos
            );
            Profiler.EndSample();

            for (var i = 0; i < componentLists.Count; i++)
            {
                var componentList = componentLists[i];
                var synchronizedComponentInfo = NetLib.synchronizedComponentInfos[i];
                var componentType = synchronizedComponentInfo.StateType;
                var oldComponentList = oldComponentLists[i];

                Profiler.BeginSample("ClientApplyState");
                ApplyState(synchronizedComponentInfo, oldComponentList, componentList);
                Profiler.EndSample();
            }
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

        var playerObjectComponent = PlayerSystem.Instance.FindPlayerObjectComponent(playerId);
        ShowWeaponFireEffects(playerObjectComponent, shotRay);
    }

    [Rpc(ExecuteOn = NetworkLibrary.NetworkPeerType.Client)]
    public void ClientOnDetonateGrenade(uint id, Vector3 position, GrenadeType type)
    {
        ShowGrenadeExplosion(position, type);
    }

    [Rpc(ExecuteOn = NetworkLibrary.NetworkPeerType.Client)]
    public void ClientOnDetonateRocket(uint id, Vector3 position)
    {
        ShowRocketExplosion(position);
    }

    [Rpc(ExecuteOn = NetworkLibrary.NetworkPeerType.Client)]
    public void ClientOnReceiveChatMessage(uint? playerId, string message)
    {
        if (playerId.HasValue)
        {
            var playerName = PlayerSystem.Instance.FindPlayerComponent(playerId.Value)?.State.Name;
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

        var playerObjectComponent = PlayerSystem.Instance.FindPlayerObjectComponent(playerId);
        if (playerObjectComponent == null) return;

        SwitchWeapons(playerObjectComponent, weaponIndex);
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

        var playerObjectComponent = PlayerSystem.Instance.FindPlayerObjectComponent(PlayerId.Value);
        if (playerObjectComponent == null) return;

        ClientPeer.CallRpcOnServer("ServerOnReceivePlayerInput", unreliableStateUpdateChannelId, new
        {
            playerId = PlayerId.Value,
            playerInput = playerObjectComponent.State.Input,
            lookDirAngles = playerObjectComponent.State.LookDirAngles
        });
    }
}