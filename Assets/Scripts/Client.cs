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
            foreach (var playerState in CurrentGameState.Players)
            {
                UpdatePlayer(playerState);
            }

            SendInputPeriodicFunction.TryToCall();
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
        if (!Input.GetKey(KeyCode.Tab))
        {
            var playerState = CurrentGameState.Players.FirstOrDefault(ps => ps.Id == PlayerId);

            if (playerState != null)
            {
                GUI.Label(new Rect(10, 10, 100, 30), "Health: " + playerState.Health);

                if (playerState.CurrentWeapon != null)
                {
                    var weapon = playerState.CurrentWeapon;
                    GUI.Label(
                        new Rect(110, 10, 100, 30),
                        "Ammo: " + weapon.BulletsLeftInMagazine + " / " + weapon.BulletsLeftOutOfMagazine
                    );
                }
            }
        }
        else
        {
            DrawScoreBoard(new Vector2(100, 100));
        }
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
    #endregion

    private int reliableSequencedChannelId;
    private int reliableChannelId;
    private int unreliableStateUpdateChannelId;
    private ThrottledAction SendInputPeriodicFunction;
    
    private void UpdatePlayer(PlayerState playerState)
    {
        if (playerState.Id == PlayerId)
        {
            playerState.Input = OsFps.Instance.GetCurrentPlayersInput();

            var mouseSensitivity = 3;
            var deltaMouse = mouseSensitivity * new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));

            playerState.LookDirAngles = new Vector2(
                Mathf.Clamp(MathfExtensions.ToSignedAngleDegrees(playerState.LookDirAngles.x - deltaMouse.y), -90, 90),
                Mathf.Repeat(playerState.LookDirAngles.y + deltaMouse.x, 360)
            );

            if (Input.GetKeyDown(KeyCode.R))
            {
                Reload(playerState);
            }

            if (Input.GetMouseButtonDown(OsFps.FireMouseButtonNumber) && playerState.CanShoot)
            {
                Shoot(playerState);
            }
        }

        OsFps.Instance.UpdatePlayer(playerState);
    }
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
        // TODO: implement weapon changing
        var playerComponent = OsFps.Instance.FindPlayerComponent(playerState.Id);

        GameObject weaponObject = Object.Instantiate(OsFps.Instance.PistolPrefab, Vector3.zero, Quaternion.identity);
        weaponObject.transform.SetParent(playerComponent.HandsPointObject.transform, false);

        var weaponComponent = weaponObject.GetComponent<WeaponComponent>();
        Object.Destroy(weaponComponent.Rigidbody);
        Object.Destroy(weaponComponent.Collider);
    }
    private void Reload(PlayerState playerState)
    {
        var message = new ReloadPressedMessage { PlayerId = playerState.Id };
        ClientPeer.SendMessageToServer(
            reliableChannelId, NetworkSerializationUtils.SerializeWithType(message)
        );
    }
    private void Shoot(PlayerState playerState)
    {
        GameObject muzzleFlashObject = Object.Instantiate(
            OsFps.Instance.MuzzleFlashPrefab, Vector3.zero, Quaternion.identity
        );
        var playerComponent = OsFps.Instance.FindPlayerComponent(playerState.Id);
        var barrelExitObject = playerComponent.HandsPointObject.FindDescendant("BarrelExit");
        muzzleFlashObject.transform.SetParent(barrelExitObject.transform, false);

        Object.Destroy(muzzleFlashObject, OsFps.MuzzleFlashDuration);

        var message = new TriggerPulledMessage { PlayerId = playerState.Id };
        ClientPeer.SendMessageToServer(
            reliableChannelId, NetworkSerializationUtils.SerializeWithType(message)
        );
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
    private Vector3 CorrectedVelocity(Vector3 serverVelocity, float roundTripTime, Vector3 clientVelocity)
    {
        var serverToClientLatency = roundTripTime / 2;
        var percentOfDiffToCorrect = 1f / 2;
        var velocityDiff = percentOfDiffToCorrect * (serverVelocity - clientVelocity);
        return clientVelocity + velocityDiff;
    }

    private void GetChanges<T>(
        List<T> oldList,
        List<T> newList,
        System.Func<T, T, bool> doElementsMatch,
        out IEnumerable<T> removedElements,
        out IEnumerable<T> addedElements,
        out IEnumerable<T> updatedElements
    )
    {
        removedElements = oldList.Where(oldElement =>
            !newList.Any(newElement => doElementsMatch(oldElement, newElement))
        );
        addedElements = newList.Where(newElement =>
            !oldList.Any(oldElement => doElementsMatch(oldElement, newElement))
        );
        updatedElements = newList.Where(newElement =>
            oldList.Any(oldElement => doElementsMatch(oldElement, newElement))
        );
    }

    private void ApplyGameState(GameState newGameState)
    {
        if (!PlayerId.HasValue) return; // Wait until the player ID has been set.

        ApplyPlayerStates(newGameState);
        ApplyDynamicObjectStates(newGameState);
    }

    private void ApplyPlayerStates(GameState newGameState)
    {
        IEnumerable<PlayerState> removedPlayerStates, addedPlayerStates, updatedPlayerStates;
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
        }

        // Update state.
        if (isPlayerMe)
        {
            updatedPlayerState.Input = currentPlayerState.Input;
        }

        CurrentGameState.Players[currentPlayerStateIndex] = updatedPlayerState;
    }
    
    private void ApplyDynamicObjectStates(GameState newGameState)
    {
        IEnumerable<DynamicObjectState> removedDynamicObjectStates, addedDynamicObjectStates, updatedDynamicObjectStates;
        GetChanges(
            CurrentGameState.DynamicObjects, newGameState.DynamicObjects, (d1, d2) => d1.Id == d2.Id,
            out removedDynamicObjectStates, out addedDynamicObjectStates, out updatedDynamicObjectStates
        );
        /*
        // Despawn dynamic objects.
        foreach (var removedDynamicObjectState in removedDynamicObjectStates)
        {
            Object.Destroy(OsFps.Instance.FindDynamicObjectObject(removedDynamicObjectState.Id));
            CurrentGameState.DynamicObjects.RemoveAll(ps => ps.Id == removedDynamicObjectState.Id);
        }

        // Spawn dynamic objects.
        foreach (var addedDynamicObjectState in addedDynamicObjectStates)
        {
            CurrentGameState.DynamicObjects.Add(addedDynamicObjectState);
            SpawnDynamicObject(addedDynamicObjectState);
        }

        // Update dynamic objects.
        foreach (var updatedDynamicObjectState in updatedDynamicObjectStates)
        {
            ApplyDynamicObjectState(updatedDynamicObjectState);
        }*/
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