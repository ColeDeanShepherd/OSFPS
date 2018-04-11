using NetLib;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Client
{
    public ClientPeer ClientPeer;
    public GameObject Camera;

    public uint PlayerId;
    public bool IsServerRemote;
    public GameState CurrentGameState;

    public void Start(bool isServerRemote)
    {
        IsServerRemote = isServerRemote;

        CurrentGameState = new GameState();

        ClientPeer = new ClientPeer();
        ClientPeer.OnReceiveDataFromServer += OnReceiveDataFromServer;

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
        ClientPeer.StartConnectingToServer(serverIpv4Address, serverPortNumber);
    }
    public void DisconnectFromServer()
    {
        ClientPeer.DisconnectFromServer();
    }

    public void Update()
    {
        ClientPeer.ReceiveAndHandleNetworkEvents();
        SendInputPeriodicFunction.TryToCall();
    }

    private void AttachCameraToPlayer(uint playerId)
    {
        var playerObject = OsFps.Instance.FindPlayerObject(playerId);
        var cameraPointObject = playerObject.GetComponent<PlayerComponent>().cameraPointObject;

        Camera.transform.position = Vector3.zero;
        Camera.transform.rotation = Quaternion.identity;

        Camera.transform.SetParent(cameraPointObject.transform, false);
    }

    private int reliableSequencedChannelId;
    private int unreliableStateUpdateChannelId;
    private ThrottledAction SendInputPeriodicFunction;

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
            case NetworkMessageType.SpawnPlayer:
                var spawnPlayerMessage = new SpawnPlayerMessage();
                spawnPlayerMessage.Deserialize(reader);

                HandleSpawnPlayerMessage(spawnPlayerMessage);
                break;
            case NetworkMessageType.PlayerInput:
                var playerInputMessage = new PlayerInputMessage();
                playerInputMessage.Deserialize(reader);

                HandlePlayerInputMessage(playerInputMessage);
                break;
            default:
                throw new System.NotImplementedException("Unknown message type: " + messageType);
        }
    }
    private void HandleSetPlayerIdMessage(SetPlayerIdMessage message)
    {
        PlayerId = message.PlayerId;

        CurrentGameState.Players.Add(new PlayerState
        {
            Id = message.PlayerId
        });
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
                Id = message.PlayerId,
                Position = message.PlayerPosition,
                EulerAngles = new Vector3(0, message.PlayerYAngle, 0),
                Input = new PlayerInput()
            };

            CurrentGameState.Players.Add(playerState);
        }

        var playerObject = IsServerRemote
            ? OsFps.Instance.SpawnLocalPlayer(playerState, message.PlayerPosition, message.PlayerYAngle)
            : OsFps.Instance.FindPlayerObject(playerState.Id);

        if (isSpawningMe)
        {
            AttachCameraToPlayer(playerState.Id);
        }
    }
    private void HandlePlayerInputMessage(PlayerInputMessage message)
    {
        if (message.PlayerId != OsFps.Instance.CurrentPlayerId)
        {
            var playerObject = OsFps.Instance.FindPlayerObject(message.PlayerId);
            
            if(playerObject != null)
            {
                playerObject
                    .GetComponent<PlayerComponent>()
                    .CurrentInput = message.PlayerInput;
            }
        }
    }

    private void SendPlayerInput()
    {
        var playerObject = OsFps.Instance.FindPlayerObject(PlayerId);
        var playerComponent = (playerObject != null) ? playerObject.GetComponent<PlayerComponent>() : null;
        if (playerComponent == null) return;

        var message = new PlayerInputMessage
        {
            PlayerId = PlayerId,
            PlayerInput = playerComponent.CurrentInput
        };

        var serializedMessage = NetworkSerializationUtils.SerializeWithType(message);
        ClientPeer.SendMessageToServer(unreliableStateUpdateChannelId, serializedMessage);
    }
}