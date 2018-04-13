using NetLib;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;

public class Client
{
    public ClientPeer ClientPeer;
    public GameObject Camera;

    public uint? PlayerId;
    public GameState CurrentGameState;
    
    public event ClientPeer.ServerConnectionChangeEventHandler OnDisconnectedFromServer;

    public void Start(bool isServerRemote)
    {
        CurrentGameState = new GameState();

        ClientPeer = new ClientPeer();
        ClientPeer.OnReceiveDataFromServer += OnReceiveDataFromServer;
        ClientPeer.OnDisconnectedFromServer += InternalOnDisconnectedFromServer;

        var connectionConfig = OsFps.Instance.CreateConnectionConfig(
            out reliableSequencedChannelId, out unreliableStateUpdateChannelId
        );
        ClientPeer.Start(connectionConfig);

        SendInputPeriodicFunction = new ThrottledAction(SendPlayerInput, 1.0f / 30);

        Camera = Object.Instantiate(OsFps.Instance.CameraPrefab);
    }
    public void Stop()
    {
        ClientPeer.Stop();
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
        if (playerState == null) return;

        var playerComponent = OsFps.Instance.FindPlayerComponent(playerState.Id);

        playerState.Position = playerComponent.transform.position;
        playerState.Velocity = playerComponent.Rigidbody.velocity;
        playerState.EulerAngles = OsFps.Instance.GetPlayerEulerAngles(playerComponent);
    }
    
    private int reliableSequencedChannelId;
    private int unreliableStateUpdateChannelId;
    private ThrottledAction SendInputPeriodicFunction;

    private void UpdatePlayer(PlayerState playerState)
    {
        if (playerState.Id == PlayerId)
        {
            playerState.Input = OsFps.Instance.GetCurrentPlayersInput();

            var mouseSensitivity = 3;
            var deltaMouse = mouseSensitivity * new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));

            playerState.EulerAngles = new Vector3(
                Mathf.Clamp(MathfExtensions.ToSignedAngleDegrees(playerState.EulerAngles.x - deltaMouse.y), -90, 90),
                Mathf.Repeat(playerState.EulerAngles.y + deltaMouse.x, 360),
                0
            );
        }

        OsFps.Instance.UpdatePlayer(playerState);
    }
    private void AttachCameraToPlayer(uint playerId)
    {
        var playerObject = OsFps.Instance.FindPlayerObject(playerId);
        var cameraPointObject = playerObject.GetComponent<PlayerComponent>().CameraPointObject;

        Camera.transform.position = Vector3.zero;
        Camera.transform.rotation = Quaternion.identity;

        Camera.transform.SetParent(cameraPointObject.transform, false);
    }
    private Vector3 CorrectedPosition(Vector3 serverPosition, Vector3 serverVelocity, float roundTripTime, Vector3 clientPosition)
    {
        var serverToClientLatency = roundTripTime / 2;
        var predictedPosition = serverPosition + (serverToClientLatency * serverVelocity);
        var positionDifference = predictedPosition - clientPosition;
        var positionDelta = 0.1f * positionDifference;
        return clientPosition + positionDelta;
    }
    private Vector3 CorrectedVelocity(Vector3 serverVelocity, float roundTripTime, Vector3 clientVelocity)
    {
        var serverToClientLatency = roundTripTime / 2;
        var velocityDiff = 0.5f * (serverVelocity - clientVelocity);
        return clientVelocity + velocityDiff;
    }
    private void ApplyGameState(GameState newGameState)
    {
        if (!PlayerId.HasValue) return; // Wait until the player ID has been set.

        // Despawn players.
        var removedPlayerIds = CurrentGameState.Players
            .Where(curPs  => !newGameState.Players.Any(newPs => newPs.Id == curPs.Id))
            .Select(curPs => curPs.Id)
            .ToList();

        CurrentGameState.Players.RemoveAll(ps => removedPlayerIds.Contains(ps.Id));
        foreach (var playerId in removedPlayerIds)
        {
            Object.Destroy(OsFps.Instance.FindPlayerObject(playerId));
        }

        // Spawn players.
        var newPlayerStates = newGameState.Players
            .Where(newPs => !CurrentGameState.Players.Any(curPs => curPs.Id == newPs.Id))
            .ToList();
        foreach (var newPlayerState in newPlayerStates)
        {
            CurrentGameState.Players.Add(newPlayerState);
            OsFps.Instance.SpawnLocalPlayer(newPlayerState);

            if (newPlayerState.Id == PlayerId)
            {
                AttachCameraToPlayer(newPlayerState.Id);
            }
        }

        var updatedPlayerStates = newGameState.Players
            .Where(newPs => !newPlayerStates.Any(spawnedPs => spawnedPs.Id == newPs.Id));
        foreach (var updatedPlayerState in updatedPlayerStates)
        {
            var currentPlayerStateIndex = CurrentGameState.Players
                .FindIndex(curPs => curPs.Id == updatedPlayerState.Id);

            // Update the player.
            var playerObject = OsFps.Instance.FindPlayerObject(updatedPlayerState.Id);
            var playerComponent = playerObject.GetComponent<PlayerComponent>();

            // Correct position.
            var roundTripTime = ClientPeer.RoundTripTime.Value;
            var correctedPosition = CorrectedPosition(
                updatedPlayerState.Position, updatedPlayerState.Velocity,
                roundTripTime, playerComponent.transform.position
            );
            playerComponent.transform.position = correctedPosition;
            updatedPlayerState.Position = correctedPosition;

            // Set velocity.
            var correctedVelocity = CorrectedVelocity(
                updatedPlayerState.Velocity, roundTripTime, playerComponent.Rigidbody.velocity
            );
            playerComponent.Rigidbody.velocity = correctedVelocity;
            updatedPlayerState.Velocity = correctedVelocity;

            // Update state & rotate.
            if (updatedPlayerState.Id != PlayerId)
            {
                CurrentGameState.Players[currentPlayerStateIndex] = updatedPlayerState;
                OsFps.Instance.ApplyEulerAnglesToPlayer(playerComponent, updatedPlayerState.EulerAngles);
            }
        }
    }

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
        playerState.EulerAngles = new Vector3(0, message.PlayerYAngle, 0);

        var playerObject = OsFps.Instance.SpawnLocalPlayer(playerState);

        if (isSpawningMe)
        {
            AttachCameraToPlayer(playerState.Id);
        }
    }

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
            EulerAngles = playerState.EulerAngles
        };

        var serializedMessage = NetworkSerializationUtils.SerializeWithType(message);
        ClientPeer.SendMessageToServer(unreliableStateUpdateChannelId, serializedMessage);
    }
}