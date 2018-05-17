using NetLib;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;

public class Client
{
    public ClientPeer ClientPeer;
    public uint? PlayerId;
    public GameState CurrentGameState;
    public GameObject Camera;
    public GameObject GuiContainer;

    public event ClientPeer.ServerConnectionChangeEventHandler OnDisconnectedFromServer;

    public PlayerSystem playerSystem = new PlayerSystem();

    public void Start(bool isServerRemote)
    {
        CurrentGameState = new GameState();

        ClientPeer = new ClientPeer();
        ClientPeer.OnReceiveDataFromServer += OnReceiveDataFromServer;
        ClientPeer.OnDisconnectedFromServer += InternalOnDisconnectedFromServer;

        var connectionConfig = OsFps.Instance.CreateConnectionConfig(
            out reliableSequencedChannelId,
            out reliableChannelId,
            out unreliableStateUpdateChannelId
        );
        ClientPeer.Start(connectionConfig);

        SendInputPeriodicFunction = new ThrottledAction(SendPlayerInput, 1.0f / 30);

        Camera = Object.Instantiate(OsFps.Instance.CameraPrefab);

        CreateGui();

        Cursor.lockState = CursorLockMode.Locked;
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
            Debug.LogError(errorMessage);
        }
    }
    public void DisconnectFromServer()
    {
        var networkError = ClientPeer.DisconnectFromServer();

        if (networkError != NetworkError.Ok)
        {
            Debug.LogError(string.Format("Failed disconnecting from server. Error: {0}", networkError));
        }
    }

    public void Update()
    {
        ClientPeer.ReceiveAndHandleNetworkEvents();

        if (ClientPeer.IsConnectedToServer)
        {
            playerSystem.OnUpdate();

            SendInputPeriodicFunction.TryToCall();
        }

        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            var playerState = CurrentGameState.Players.FirstOrDefault(ps => ps.Id == PlayerId);
            SwitchWeapons(playerState, 0);
        }

        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            var playerState = CurrentGameState.Players.FirstOrDefault(ps => ps.Id == PlayerId);
            SwitchWeapons(playerState, 1);
        }

        if (Input.GetKeyDown(OsFps.ChatKeyCode))
        {
            if (!_isShowingChatMessageInput)
            {
                _isShowingChatMessageInput = true;
                _justOpenedChatMessageInput = true;
                _chatMessageBeingTyped = "";

                Cursor.lockState = CursorLockMode.None;
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
                Cursor.lockState = CursorLockMode.None;
            }

            _isShowingMenu = !_isShowingMenu;
        }

        if (!_isShowingChatMessageInput && !_isShowingMenu)
        {
            Cursor.lockState = CursorLockMode.Locked;
        }
    }
    public void LateUpdate()
    {
        var playerState = CurrentGameState.Players.FirstOrDefault(ps => ps.Id == PlayerId);
        if (playerState != null)
        {
            var playerComponent = OsFps.Instance.FindPlayerComponent(playerState.Id);
            if (playerComponent != null)
            {
                playerState.Position = playerComponent.transform.position;
                playerState.Velocity = playerComponent.Rigidbody.velocity;
                playerState.LookDirAngles = OsFps.Instance.GetPlayerLookDirAngles(playerComponent);
            }
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
        if (!Input.GetKey(OsFps.ShowScoreboardKeyCode))
        {
            DrawHud();
        }
        else
        {
            DrawScoreBoard(new Vector2(100, 100));
        }

        if (_isShowingMenu)
        {
            DrawMenu();
        }

        DrawChatWindow(new Vector2(100, Screen.height - 500));
    }
    private void DrawHud()
    {
        var playerState = CurrentGameState.Players.FirstOrDefault(ps => ps.Id == PlayerId);

        if (playerState != null)
        {
            GUI.Label(new Rect(10, 10, 100, 30), "Health: " + playerState.Health);

            var weaponHudX = 110;
            var weaponHudY = 10;
            var weaponHudHeight = 110;

            if (playerState.CurrentWeapon != null)
            {
                DrawWeaponHud(playerState.CurrentWeapon, new Vector2(weaponHudX, weaponHudY));
                weaponHudY += weaponHudHeight;
            }

            foreach (var weapon in playerState.Weapons)
            {
                if ((weapon != null) && (weapon != playerState.CurrentWeapon))
                {
                    DrawWeaponHud(weapon, new Vector2(weaponHudX, weaponHudY));
                    weaponHudY += weaponHudHeight;
                }
            }
        }
    }
    private void DrawWeaponHud(WeaponState weapon, Vector2 position)
    {
        GUI.Label(
            new Rect(position.x, position.y, 200, 50),
            weapon.Type.ToString() + " Ammo: " + weapon.BulletsLeftInMagazine + " / " + weapon.BulletsLeftOutOfMagazine
        );
    }
    private void DrawScoreBoard(Vector2 position)
    {
        const float playerIdColumnWidth = 50;
        const float killsColumnWidth = 100;
        const float deathsColumnWidth = 100;
        const float rowHeight = 50;

        var idColumnX = position.x;
        var killsColumnX = idColumnX + playerIdColumnWidth;
        var deathsColumnX = killsColumnX + killsColumnWidth;

        // Draw table header.
        GUI.Label(new Rect(idColumnX, position.y, playerIdColumnWidth, rowHeight), "ID");
        GUI.Label(new Rect(killsColumnX, position.y, killsColumnWidth, rowHeight), "Kills");
        GUI.Label(new Rect(deathsColumnX, position.y, deathsColumnWidth, rowHeight), "Deaths");
        position.y += rowHeight;

        // Draw player rows.
        foreach (var playerState in CurrentGameState.Players)
        {
            GUI.Label(new Rect(idColumnX, position.y, playerIdColumnWidth, rowHeight), playerState.Id.ToString());
            GUI.Label(new Rect(killsColumnX, position.y, killsColumnWidth, rowHeight), playerState.Kills.ToString());
            GUI.Label(new Rect(deathsColumnX, position.y, deathsColumnWidth, rowHeight), playerState.Deaths.ToString());
            position.y += rowHeight;
        }
    }
    private void DrawChatWindow(Vector2 position)
    {
        var width = 300;
        var lineHeight = 50;

        var chatMessagesRect = new Rect(position, new Vector2(width, 300));

        for (var i = 0; i < _chatMessages.Count; i++)
        {
            var chatMessageIndex = (_chatMessages.Count - 1) - i;
            var y = chatMessagesRect.yMax - ((i + 1) * lineHeight);
            GUI.Label(new Rect(new Vector2(position.x, y), new Vector2(width, lineHeight)), _chatMessages[chatMessageIndex]);
        }

        if (_isShowingChatMessageInput)
        {
            if ((Event.current.type == EventType.KeyDown) && (Event.current.keyCode == KeyCode.Return))
            {
                ConfirmChatMessage();
            }

            GUI.SetNextControlName("chatMessageInput");
            var chatMessageTextFieldRect = new Rect(new Vector3(position.x, chatMessagesRect.yMax), new Vector2(width, lineHeight));
            _chatMessageBeingTyped = GUI.TextField(chatMessageTextFieldRect, _chatMessageBeingTyped);
        }

        if (_justOpenedChatMessageInput)
        {
            GUI.FocusControl("chatMessageInput");
            _justOpenedChatMessageInput = false;
        }
    }
    private void DrawMenu()
    {
        var buttonSize = new Vector2(100, 50);
        var buttonVerticalSpacing = 10;
        var curPosition = new Vector2(300, 100);

        if (GUI.Button(new Rect(curPosition, buttonSize), "Exit Menu"))
        {
            _isShowingMenu = false;
        }

        curPosition.y += buttonSize.y + buttonVerticalSpacing;

        if (GUI.Button(new Rect(curPosition, buttonSize), "Quit"))
        {
            DisconnectFromServer();
        }
    }
    #endregion

    private int reliableSequencedChannelId;
    private int reliableChannelId;
    private int unreliableStateUpdateChannelId;
    private ThrottledAction SendInputPeriodicFunction;

    public bool _isShowingChatMessageInput;
    public bool _justOpenedChatMessageInput;
    private string _chatMessageBeingTyped;
    private List<string> _chatMessages = new List<string>();

    public bool _isShowingMenu;
    
    private void AttachCameraToPlayer(uint playerId)
    {
        var playerObject = OsFps.Instance.FindPlayerObject(playerId);
        var cameraPointObject = playerObject.GetComponent<PlayerComponent>().CameraPointObject;
        
        Camera.transform.SetParent(cameraPointObject.transform);

        Camera.transform.localPosition = Vector3.zero;
        Camera.transform.localRotation = Quaternion.identity;
    }
    private void DetachCameraFromPlayer()
    {
        Camera.transform.SetParent(null, true);
    }
    private void EquipWeapon(PlayerState playerState)
    {
        if (playerState.CurrentWeapon == null)
        {
            return;
        }

        var playerComponent = OsFps.Instance.FindPlayerComponent(playerState.Id);

        var weaponPrefab = OsFps.Instance.GetWeaponPrefab(playerState.CurrentWeapon.Type);
        GameObject weaponObject = Object.Instantiate(weaponPrefab, Vector3.zero, Quaternion.identity);
        weaponObject.transform.SetParent(playerComponent.HandsPointObject.transform, false);

        var weaponComponent = weaponObject.GetComponent<WeaponComponent>();
        Object.Destroy(weaponComponent.Rigidbody);
        Object.Destroy(weaponComponent.Collider);
    }
    public void Reload(PlayerState playerState)
    {
        var message = new ReloadPressedMessage { PlayerId = playerState.Id };
        ClientPeer.SendMessageToServer(
            reliableChannelId, NetworkSerializationUtils.SerializeWithType(message)
        );
    }

    public void ShowMuzzleFlash(PlayerState playerState)
    {
        GameObject muzzleFlashObject = Object.Instantiate(
            OsFps.Instance.MuzzleFlashPrefab, Vector3.zero, Quaternion.identity
        );
        var playerComponent = OsFps.Instance.FindPlayerComponent(playerState.Id);
        var barrelExitObject = playerComponent.HandsPointObject.FindDescendant("BarrelExit");
        muzzleFlashObject.transform.SetParent(barrelExitObject.transform, false);

        Object.Destroy(muzzleFlashObject, OsFps.MuzzleFlashDuration);
    }
    public void ShowGrenadeExplosion(Vector3 position, GrenadeType grenadeType)
    {
        var explosionPrefab = OsFps.Instance.GetGrenadeExplosionPrefab(grenadeType);
        GameObject grenadeExplosionObject = Object.Instantiate(
            explosionPrefab, position, Quaternion.identity
        );

        Object.Destroy(grenadeExplosionObject, OsFps.GrenadeExplosionDuration);
    }
    public void Shoot(PlayerState playerState)
    {
        ShowMuzzleFlash(playerState);

        var message = new TriggerPulledMessage { PlayerId = playerState.Id };
        ClientPeer.SendMessageToServer(
            reliableChannelId, NetworkSerializationUtils.SerializeWithType(message)
        );

        playerState.CurrentWeapon.TimeUntilCanShoot = playerState.CurrentWeapon.Definition.ShotInterval;
    }

    public void ThrowGrenade(PlayerState playerState)
    {
        var message = new ThrowGrenadeMessage { PlayerId = playerState.Id };
        ClientPeer.SendMessageToServer(
            reliableChannelId, NetworkSerializationUtils.SerializeWithType(message)
        );

        playerState.TimeUntilCanThrowGrenade = OsFps.GrenadeThrowInterval;
    }

    private void SwitchWeapons(PlayerState playerState, int weaponIndex)
    {
        if (weaponIndex == playerState.CurrentWeaponIndex) return;

        // destroy weapon obj
        if (playerState.CurrentWeapon != null)
        {
            var playerComponent = OsFps.Instance.FindPlayerComponent(playerState.Id);
            var weaponComponent = playerComponent.HandsPointObject.GetComponentInChildren<WeaponComponent>();

            if (weaponComponent != null)
            {
                Object.Destroy(weaponComponent.gameObject);
            }
        }

        playerState.CurrentWeaponIndex = (byte)weaponIndex;

        EquipWeapon(playerState);

        // Send message to server.
        var message = new ChangeWeaponMessage
        {
            PlayerId = playerState.Id,
            WeaponIndex = (byte)weaponIndex
        };
        ClientPeer.SendMessageToServer(reliableSequencedChannelId, NetworkSerializationUtils.SerializeWithType(message));
    }

    private void ConfirmChatMessage()
    {
        if (_chatMessageBeingTyped.Length > 0)
        {
            var message = new ChatMessage
            {
                PlayerId = PlayerId.Value,
                Message = _chatMessageBeingTyped
            };
            ClientPeer.SendMessageToServer(
                reliableChannelId, NetworkSerializationUtils.SerializeWithType(message)
            );

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
        var serverToClientLatency = roundTripTime / 2;
        var predictedEulerAngles = serverEulerAngles + (serverToClientLatency * serverAngularVelocity);
        var eulerAnglesDifference = predictedEulerAngles - clientEulerAngles;
        var percentOfDiffToCorrect = 1f / 3;
        var eulerAnglesDelta = percentOfDiffToCorrect * eulerAnglesDifference;
        return clientEulerAngles + eulerAnglesDelta;
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

    private void ApplyGameState(GameState newGameState)
    {
        if (!PlayerId.HasValue) return; // Wait until the player ID has been set.

        ApplyPlayerStates(newGameState);
        ApplyWeaponObjectStates(newGameState);
        ApplyGrenadeStates(newGameState);
    }

    private void ApplyPlayerStates(GameState newGameState)
    {
        List<PlayerState> removedPlayerStates, addedPlayerStates, updatedPlayerStates;
        GetChanges(
            CurrentGameState.Players, newGameState.Players, (p1, p2) => p1.Id == p2.Id,
            out removedPlayerStates, out addedPlayerStates, out updatedPlayerStates
        );

        // Despawn players.
        foreach (var removedPlayerState in removedPlayerStates)
        {
            Object.Destroy(OsFps.Instance.FindPlayerObject(removedPlayerState.Id));
            CurrentGameState.Players.RemoveAll(ps => ps.Id == removedPlayerState.Id);
        }

        // Spawn players.
        foreach (var addedPlayerState in addedPlayerStates)
        {
            CurrentGameState.Players.Add(addedPlayerState);
            SpawnPlayer(addedPlayerState);
        }

        // Update existing players.
        foreach (var updatedPlayerState in updatedPlayerStates)
        {
            ApplyPlayerState(updatedPlayerState);
        }
    }
    private void ApplyPlayerState(PlayerState updatedPlayerState)
    {
        var currentPlayerStateIndex = CurrentGameState.Players
                .FindIndex(curPs => curPs.Id == updatedPlayerState.Id);
        var currentPlayerState = CurrentGameState.Players[currentPlayerStateIndex];

        var isPlayerMe = updatedPlayerState.Id == PlayerId;
        var roundTripTime = ClientPeer.RoundTripTime.Value;

        var playerObject = OsFps.Instance.FindPlayerObject(updatedPlayerState.Id);

        // Handle player killed.
        if ((playerObject != null) && !updatedPlayerState.IsAlive)
        {
            if (isPlayerMe)
            {
                DetachCameraFromPlayer();
            }

            Object.Destroy(playerObject);
        }

        // Handle player spawned.
        if ((playerObject == null) && updatedPlayerState.IsAlive)
        {
            SpawnPlayer(updatedPlayerState);

            if (isPlayerMe)
            {
                AttachCameraToPlayer(updatedPlayerState.Id);
            }
        }

        // Handle weapon pickup.
        if (
            (playerObject != null) &&
            (updatedPlayerState.CurrentWeaponIndex == currentPlayerState.CurrentWeaponIndex) &&
            (updatedPlayerState.CurrentWeapon != null) &&
            (currentPlayerState.CurrentWeapon == null)
        )
        {
            EquipWeapon(updatedPlayerState);
        }

        // Update player object.
        if (playerObject != null)
        {
            var playerComponent = playerObject.GetComponent<PlayerComponent>();

            // Correct position.
            var correctedPosition = CorrectedPosition(
                updatedPlayerState.Position, updatedPlayerState.Velocity,
                roundTripTime, playerComponent.transform.position
            );
            playerComponent.transform.position = correctedPosition;
            updatedPlayerState.Position = correctedPosition;

            // Correct velocity.
            var correctedVelocity = CorrectedVelocity(
                updatedPlayerState.Velocity, roundTripTime, playerComponent.Rigidbody.velocity
            );
            playerComponent.Rigidbody.velocity = correctedVelocity;
            updatedPlayerState.Velocity = correctedVelocity;

            // Update look direction.
            if (isPlayerMe)
            {
                updatedPlayerState.LookDirAngles = OsFps.Instance.GetPlayerLookDirAngles(playerComponent);
            }

            OsFps.Instance.ApplyLookDirAnglesToPlayer(playerComponent, updatedPlayerState.LookDirAngles);

            // Update weapon if reloading.
            var weaponComponent = playerComponent.GetComponentInChildren<WeaponComponent>();
            var weaponGameObject = (weaponComponent != null) ? weaponComponent.gameObject : null;
            if ((weaponGameObject != null) && updatedPlayerState.IsReloading)
            {
                var percentDoneReloading = updatedPlayerState.ReloadTimeLeft / updatedPlayerState.CurrentWeapon.Definition.ReloadTime;

                var y = -(1.0f - Mathf.Abs((2 * percentDoneReloading) - 1));
                weaponGameObject.transform.localPosition = new Vector3(0, y, 0);
            }
        }

        // Update state.
        if (isPlayerMe)
        {
            if ((updatedPlayerState.CurrentWeapon != null) && (currentPlayerState.CurrentWeapon != null))
            {
                updatedPlayerState.CurrentWeapon.TimeUntilCanShoot = currentPlayerState.CurrentWeapon.TimeUntilCanShoot;
            }

            updatedPlayerState.TimeUntilCanThrowGrenade = currentPlayerState.TimeUntilCanThrowGrenade;

            updatedPlayerState.Input = currentPlayerState.Input;
        }

        CurrentGameState.Players[currentPlayerStateIndex] = updatedPlayerState;
    }
    
    private void ApplyWeaponObjectStates(GameState newGameState)
    {
        List<WeaponObjectState> removedWeaponObjectStates, addedWeaponObjectStates, updatedWeaponObjectStates;
        GetChanges(
            CurrentGameState.WeaponObjects, newGameState.WeaponObjects, (wo1, wo2) => wo1.Id == wo2.Id,
            out removedWeaponObjectStates, out addedWeaponObjectStates, out updatedWeaponObjectStates
        );

        // Despawn weapon objects.
        foreach (var removedWeaponObjectState in removedWeaponObjectStates)
        {
            Object.Destroy(OsFps.Instance.FindWeaponObject(removedWeaponObjectState.Id));
        }
        var removedWeaponObjectIds = removedWeaponObjectStates
            .Select(wos => wos.Id)
            .ToList();
        CurrentGameState.WeaponObjects.RemoveAll(ps => removedWeaponObjectIds.Contains(ps.Id));

        // Spawn weapon objects.
        foreach (var addedWeaponObjectState in addedWeaponObjectStates)
        {
            CurrentGameState.WeaponObjects.Add(addedWeaponObjectState);
            OsFps.Instance.SpawnLocalWeaponObject(addedWeaponObjectState);
        }

        // Update existing weapon objects.
        foreach (var updatedWeaponObjectState in updatedWeaponObjectStates)
        {
            ApplyWeaponObjectState(updatedWeaponObjectState);
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
    private void ApplyWeaponObjectState(WeaponObjectState updatedWeaponObjectState)
    {
        var currentWeaponObjectStateIndex = CurrentGameState.WeaponObjects
                .FindIndex(curPs => curPs.Id == updatedWeaponObjectState.Id);
        var currentWeaponObjectState = CurrentGameState.WeaponObjects[currentWeaponObjectStateIndex];
        var weaponObject = OsFps.Instance.FindWeaponObject(updatedWeaponObjectState.Id);

        // Update weapon object.
        if (weaponObject != null)
        {
            var weaponComponent = weaponObject.GetComponent<WeaponComponent>();
            ApplyRigidbodyState(
                updatedWeaponObjectState.RigidBodyState,
                currentWeaponObjectState.RigidBodyState,
                weaponComponent.Rigidbody
            );
        }

        // Update state.
        CurrentGameState.WeaponObjects[currentWeaponObjectStateIndex] = updatedWeaponObjectState;
    }

    private void ApplyGrenadeStates(GameState newGameState)
    {
        List<GrenadeState> removedGrenadeStates, addedGrenadeStates, updatedGrenadeStates;
        GetChanges(
            CurrentGameState.Grenades, newGameState.Grenades, (g1, g2) => g1.Id == g2.Id,
            out removedGrenadeStates, out addedGrenadeStates, out updatedGrenadeStates
        );

        // Despawn weapon objects.
        foreach (var removedGrenadeState in removedGrenadeStates)
        {
            Object.Destroy(OsFps.Instance.FindGrenade(removedGrenadeState.Id));
        }
        var removedGrenadeIds = removedGrenadeStates
            .Select(wos => wos.Id)
            .ToList();
        CurrentGameState.Grenades.RemoveAll(ps => removedGrenadeIds.Contains(ps.Id));

        // Spawn weapon objects.
        foreach (var addedGrenadeState in addedGrenadeStates)
        {
            CurrentGameState.Grenades.Add(addedGrenadeState);
            OsFps.Instance.SpawnLocalGrenadeObject(addedGrenadeState);
        }

        // Update existing weapon objects.
        foreach (var updatedGrenadeState in updatedGrenadeStates)
        {
            ApplyGrenadeState(updatedGrenadeState);
        }
    }
    private void ApplyGrenadeState(GrenadeState updatedGrenadeState)
    {
        var currentGrenadeStateIndex = CurrentGameState.Grenades
                .FindIndex(curGs => curGs.Id == updatedGrenadeState.Id);
        var currentGrenadeState = CurrentGameState.Grenades[currentGrenadeStateIndex];
        var grenadeObject = OsFps.Instance.FindGrenade(updatedGrenadeState.Id);

        // Update weapon object.
        if (grenadeObject != null)
        {
            var grenadeComponent = grenadeObject.GetComponent<GrenadeComponent>();
            ApplyRigidbodyState(
                updatedGrenadeState.RigidBodyState,
                currentGrenadeState.RigidBodyState,
                grenadeComponent.Rigidbody
            );
        }

        // Update state.
        CurrentGameState.Grenades[currentGrenadeStateIndex] = updatedGrenadeState;
    }

    private void SpawnPlayer(PlayerState playerState)
    {
        OsFps.Instance.SpawnLocalPlayer(playerState);
        EquipWeapon(playerState);

        if (playerState.Id == PlayerId)
        {
            AttachCameraToPlayer(playerState.Id);
        }
    }

    #region Message Handlers
    private void OnReceiveDataFromServer(int channelId, byte[] bytesReceived)
    {
        var reader = new BinaryReader(new MemoryStream(bytesReceived));
        var messageType = (NetworkMessageType)reader.ReadByte();

        switch(messageType)
        {
            case NetworkMessageType.SetPlayerId:
                var setPlayerIdMessage = new SetPlayerIdMessage();
                setPlayerIdMessage.Deserialize(reader);

                HandleSetPlayerIdMessage(setPlayerIdMessage);
                break;
            case NetworkMessageType.GameState:
                var gameStateMessage = new GameStateMessage();
                gameStateMessage.Deserialize(reader);

                HandleGameStateMessage(gameStateMessage);
                return;
            case NetworkMessageType.SpawnPlayer:
                var spawnPlayerMessage = new SpawnPlayerMessage();
                spawnPlayerMessage.Deserialize(reader);

                HandleSpawnPlayerMessage(spawnPlayerMessage);
                break;
            case NetworkMessageType.TriggerPulled:
                var triggerPulledMessage = new TriggerPulledMessage();
                triggerPulledMessage.Deserialize(reader);

                HandleTriggerPulledMessage(triggerPulledMessage);
                break;
            case NetworkMessageType.DetonateGrenade:
                var detonateGrenadeMessage = new DetonateGrenadeMessage();
                detonateGrenadeMessage.Deserialize(reader);

                HandleDetonateGrenadeMessage(detonateGrenadeMessage);
                break;
            case NetworkMessageType.Chat:
                var chatMessage = new ChatMessage();
                chatMessage.Deserialize(reader);

                HandleChatMessage(chatMessage);
                break;
            case NetworkMessageType.ChangeWeapon:
                var changeWeaponMessage = new ChangeWeaponMessage();
                changeWeaponMessage.Deserialize(reader);

                HandleChangeWeaponMessage(changeWeaponMessage);
                break;
            default:
                throw new System.NotImplementedException("Unknown message type: " + messageType);
        }
    }
    private void HandleSetPlayerIdMessage(SetPlayerIdMessage message)
    {
        PlayerId = message.PlayerId;

        /*CurrentGameState.Players.Add(new PlayerState
        {
            Id = message.PlayerId
        });*/
    }
    private void HandleGameStateMessage(GameStateMessage message)
    {
        ApplyGameState(message.GameState);
    }
    private void HandleSpawnPlayerMessage(SpawnPlayerMessage message)
    {
        var isSpawningMe = (message.PlayerId == PlayerId);
        var playerState = CurrentGameState.Players
            .FirstOrDefault(ps => ps.Id == message.PlayerId);

        if(playerState == null)
        {
            playerState = new PlayerState
            {
                Id = message.PlayerId
            };

            CurrentGameState.Players.Add(playerState);
        }
        
        playerState.Position = message.PlayerPosition;
        playerState.Health = OsFps.MaxPlayerHealth;
        playerState.LookDirAngles = new Vector3(0, message.PlayerLookDirYAngle, 0);

        var playerObject = OsFps.Instance.SpawnLocalPlayer(playerState);

        if (isSpawningMe)
        {
            AttachCameraToPlayer(playerState.Id);
        }
    }
    private void HandleTriggerPulledMessage(TriggerPulledMessage message)
    {
        // Don't anything if we pulled the trigger.
        if (message.PlayerId == PlayerId)
        {
            return;
        }

        var playerState = CurrentGameState.Players.First(ps => ps.Id == message.PlayerId);
        ShowMuzzleFlash(playerState);
    }
    private void HandleDetonateGrenadeMessage(DetonateGrenadeMessage message)
    {
        ShowGrenadeExplosion(message.Position, message.Type);
    }
    private void HandleChatMessage(ChatMessage message)
    {
        if (message.PlayerId.HasValue)
        {
            _chatMessages.Add(string.Format("{0}: {1}", message.PlayerId, message.Message));
        }
        else
        {
            _chatMessages.Add(message.Message);
        }
    }
    private void HandleChangeWeaponMessage(ChangeWeaponMessage message)
    {
        if (message.PlayerId == PlayerId)
        {
            return;
        }

        var playerState = CurrentGameState.Players.FirstOrDefault(ps => ps.Id == message.PlayerId);
        if (playerState == null) return;

        SwitchWeapons(playerState, message.WeaponIndex);
    }
    #endregion

    private void InternalOnDisconnectedFromServer()
    {
        if (OnDisconnectedFromServer != null)
        {
            OnDisconnectedFromServer();
        }
    }

    private void SendPlayerInput()
    {
        var playerState = CurrentGameState.Players.FirstOrDefault(ps => ps.Id == PlayerId);
        if (playerState == null) return;

        var message = new PlayerInputMessage
        {
            PlayerId = PlayerId.Value,
            PlayerInput = playerState.Input,
            LookDirAngles = playerState.LookDirAngles
        };

        var serializedMessage = NetworkSerializationUtils.SerializeWithType(message);
        ClientPeer.SendMessageToServer(unreliableStateUpdateChannelId, serializedMessage);
    }
}