using NetLib;
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
            UpdateLocalPlayer();
            SendInputPeriodicFunction.TryToCall();
        }
    }
    
    private int reliableSequencedChannelId;
    private int unreliableStateUpdateChannelId;
    private ThrottledAction SendInputPeriodicFunction;

    private void UpdateLocalPlayer()
    {
        var playerState = CurrentGameState.Players.FirstOrDefault(ps => ps.Id == PlayerId);
        if (playerState == null) return;

        playerState.Input = OsFps.Instance.GetCurrentPlayersInput();

        var mouseSensitivity = 3;
        var deltaMouse = mouseSensitivity * new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));

        playerState.EulerAngles = new Vector3(
            Mathf.Clamp(playerState.EulerAngles.x - deltaMouse.y, -90, 90),
            Mathf.Repeat(playerState.EulerAngles.y + deltaMouse.x, 360),
            0
        );

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
    private void ApplyGameState(GameState newGameState)
    {
        if (!PlayerId.HasValue) return; // Wait until the player ID has been set.

        // Despawn players.
        var removedPlayerIds = CurrentGameState.Players
            .Where(oldPs  => !newGameState.Players.Any(newPs => newPs.Id == oldPs.Id))
            .Select(ps => ps.Id)
            .ToList();

        CurrentGameState.Players.RemoveAll(ps => removedPlayerIds.Contains(ps.Id));
        foreach (var playerId in removedPlayerIds)
        {
            Object.Destroy(OsFps.Instance.FindPlayerObject(playerId));
        }

        foreach (var newPlayerState in newGameState.Players)
        {
            var isMe = newPlayerState.Id == PlayerId;
            var currentPlayerStateIndex = CurrentGameState.Players
                .FindIndex(ps => ps.Id == newPlayerState.Id);

            // Spawn the player if we haven't already.
            var needToSpawnPlayer = currentPlayerStateIndex < 0;
            if (needToSpawnPlayer)
            {
                CurrentGameState.Players.Add(newPlayerState);
                currentPlayerStateIndex = CurrentGameState.Players.Count - 1;

                OsFps.Instance.SpawnLocalPlayer(newPlayerState);

                if (isMe)
                {
                    AttachCameraToPlayer(newPlayerState.Id);
                }
            }

            // Update the player.
            var playerObject = OsFps.Instance.FindPlayerObject(newPlayerState.Id);
            var playerComponent = playerObject.GetComponent<PlayerComponent>();

            var playerNeedsCorrection = Vector3.Distance(playerObject.transform.position, newPlayerState.Position) >= 0.5f;

            if (playerNeedsCorrection)
            {
                playerObject.transform.position = newPlayerState.Position;
            }
            else
            {
                newPlayerState.Position = playerObject.transform.position;
            }

            if (!isMe)
            {
                CurrentGameState.Players[currentPlayerStateIndex] = newPlayerState;
                OsFps.Instance.ApplyEulerAnglesToPlayer(playerComponent, newPlayerState.EulerAngles);
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