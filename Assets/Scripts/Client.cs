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
    public GameObject Camera;
    public GameObject GuiContainer;

    public event ClientPeer.ServerConnectionChangeEventHandler OnDisconnectedFromServer;

    public GameStateScraperSystem gameStateScraperSystem = new GameStateScraperSystem();

    public void Start(bool isServerRemote)
    {
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
            SendInputPeriodicFunction.TryToCall();
        }

        if (PlayerId != null)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                var playerObjectComponent = OsFps.Instance.FindPlayerObjectComponent(PlayerId.Value);
                SwitchWeapons(playerObjectComponent, 0);
            }

            if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                var playerObjectComponent = OsFps.Instance.FindPlayerObjectComponent(PlayerId.Value);
                SwitchWeapons(playerObjectComponent, 1);
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
        var playerObjectComponent = PlayerId.HasValue
            ? OsFps.Instance.FindPlayerObjectComponent(PlayerId.Value)
            : null;

        if (playerObjectComponent != null)
        {
            var playerObjectState = playerObjectComponent.State;

            GUI.Label(new Rect(10, 10, 100, 30), "Health: " + playerObjectState.Health);

            var weaponHudX = 110;
            var weaponHudY = 10;
            var weaponHudHeight = 110;

            if (playerObjectState.CurrentWeapon != null)
            {
                DrawWeaponHud(playerObjectState.CurrentWeapon, new Vector2(weaponHudX, weaponHudY));
                weaponHudY += weaponHudHeight;
            }

            foreach (var weapon in playerObjectState.Weapons)
            {
                if ((weapon != null) && (weapon != playerObjectState.CurrentWeapon))
                {
                    DrawWeaponHud(weapon, new Vector2(weaponHudX, weaponHudY));
                    weaponHudY += weaponHudHeight;
                }
            }
        }
    }
    private void DrawWeaponHud(WeaponObjectState weapon, Vector2 position)
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
        var playerComponents = Object.FindObjectsOfType<PlayerComponent>();
        foreach (var playerComponent in playerComponents)
        {
            var playerState = playerComponent.State;
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

    public int reliableSequencedChannelId;
    public int reliableChannelId;
    public int unreliableStateUpdateChannelId;
    private ThrottledAction SendInputPeriodicFunction;

    public bool _isShowingChatMessageInput;
    public bool _justOpenedChatMessageInput;
    private string _chatMessageBeingTyped;
    public List<string> _chatMessages = new List<string>();

    public bool _isShowingMenu;
    
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
    private void EquipWeapon(PlayerObjectState playerState)
    {
        if (playerState.CurrentWeapon == null)
        {
            return;
        }

        var playerObjectComponent = OsFps.Instance.FindPlayerObjectComponent(playerState.Id);

        var weaponPrefab = OsFps.Instance.GetWeaponPrefab(playerState.CurrentWeapon.Type);
        GameObject weaponObject = Object.Instantiate(weaponPrefab, Vector3.zero, Quaternion.identity);
        weaponObject.transform.SetParent(playerObjectComponent.HandsPointObject.transform, false);

        var weaponComponent = weaponObject.GetComponent<WeaponComponent>();
        Object.Destroy(weaponComponent.Rigidbody);
        Object.Destroy(weaponComponent.Collider);
    }
    public void Reload(PlayerObjectState playerState)
    {
        OsFps.Instance.CallRpcOnServer("ServerOnPlayerReloadPressed", reliableChannelId, new
        {
            playerId = playerState.Id
        });
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
    public void ShowGrenadeExplosion(Vector3 position, GrenadeType grenadeType)
    {
        var explosionPrefab = OsFps.Instance.GetGrenadeExplosionPrefab(grenadeType);
        GameObject grenadeExplosionObject = Object.Instantiate(
            explosionPrefab, position, Quaternion.identity
        );

        Object.Destroy(grenadeExplosionObject, OsFps.GrenadeExplosionDuration);
    }
    public void Shoot(PlayerObjectComponent playerObjectComponent)
    {
        ShowMuzzleFlash(playerObjectComponent);

        OsFps.Instance.CallRpcOnServer("ServerOnPlayerTriggerPulled", reliableChannelId, new
        {
            playerId = playerObjectComponent.State.Id
        });

        playerObjectComponent.State.CurrentWeapon.TimeUntilCanShoot =
            playerObjectComponent.State.CurrentWeapon.Definition.ShotInterval;
    }

    public void ThrowGrenade(PlayerObjectState playerState)
    {
        OsFps.Instance.CallRpcOnServer("ServerOnPlayerThrowGrenade", reliableChannelId, new
        {
            playerId = playerState.Id
        });

        playerState.TimeUntilCanThrowGrenade = OsFps.GrenadeThrowInterval;
    }

    public void SwitchWeapons(PlayerObjectComponent playerObjectComponent, int weaponIndex)
    {
        var playerObjectState = playerObjectComponent.State;

        if (weaponIndex == playerObjectState.CurrentWeaponIndex) return;

        // destroy weapon obj
        if (playerObjectState.CurrentWeapon != null)
        {
            var weaponComponent = playerObjectComponent.HandsPointObject.GetComponentInChildren<WeaponComponent>();

            if (weaponComponent != null)
            {
                Object.Destroy(weaponComponent.gameObject);
            }
        }

        playerObjectState.CurrentWeaponIndex = (byte)weaponIndex;

        EquipWeapon(playerObjectState);

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
                playerId = PlayerId.Value,
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
        if (
            (playerObject != null) &&
            (updatedPlayerObjectState.CurrentWeaponIndex == currentPlayerObjectState.CurrentWeaponIndex) &&
            (updatedPlayerObjectState.CurrentWeapon != null) &&
            (currentPlayerObjectState.CurrentWeapon == null)
        )
        {
            EquipWeapon(updatedPlayerObjectState);
        }

        // Update player object.
        if (playerObject != null)
        {
            var playerComponent = playerObject.GetComponent<PlayerObjectComponent>();

            // Correct position.
            var correctedPosition = CorrectedPosition(
                updatedPlayerObjectState.Position, updatedPlayerObjectState.Velocity,
                roundTripTime, playerComponent.transform.position
            );
            playerComponent.transform.position = correctedPosition;
            updatedPlayerObjectState.Position = correctedPosition;

            // Correct velocity.
            var correctedVelocity = CorrectedVelocity(
                updatedPlayerObjectState.Velocity, roundTripTime, playerComponent.Rigidbody.velocity
            );
            playerComponent.Rigidbody.velocity = correctedVelocity;
            updatedPlayerObjectState.Velocity = correctedVelocity;

            // Update look direction.
            if (isPlayerMe)
            {
                updatedPlayerObjectState.LookDirAngles = OsFps.Instance.GetPlayerLookDirAngles(playerComponent);
            }

            OsFps.Instance.ApplyLookDirAnglesToPlayer(playerComponent, updatedPlayerObjectState.LookDirAngles);

            // Update weapon if reloading.
            var weaponComponent = playerComponent.GetComponentInChildren<WeaponComponent>();
            var weaponGameObject = weaponComponent?.gameObject;
            if ((weaponGameObject != null) && updatedPlayerObjectState.IsReloading)
            {
                var percentDoneReloading = updatedPlayerObjectState.ReloadTimeLeft / updatedPlayerObjectState.CurrentWeapon.Definition.ReloadTime;

                var y = -(1.0f - Mathf.Abs((2 * percentDoneReloading) - 1));
                weaponGameObject.transform.localPosition = new Vector3(0, y, 0);
            }
        }

        // Update state.
        if (isPlayerMe)
        {
            if ((updatedPlayerObjectState.CurrentWeapon != null) && (currentPlayerObjectState.CurrentWeapon != null))
            {
                updatedPlayerObjectState.CurrentWeapon.TimeUntilCanShoot = currentPlayerObjectState.CurrentWeapon.TimeUntilCanShoot;
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

    private void SpawnPlayer(PlayerObjectState playerState)
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
    #endregion

    private void InternalOnDisconnectedFromServer()
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