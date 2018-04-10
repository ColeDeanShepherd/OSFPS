using NetLib;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Client
{
    public ClientPeer ClientPeer;
    public GameObject Camera;

    public uint playerId;
    public bool isServerRemote;
    public List<RemoteClientInfo> clientInfos;

    public void Start(bool isServerRemote)
    {
        this.isServerRemote = isServerRemote;

        clientInfos = new List<RemoteClientInfo>();

        ClientPeer = new ClientPeer();
        ClientPeer.OnReceiveDataFromServer += OnReceiveDataFromServer;

        ClientPeer.Start(portNumber: null, maxConnectionCount: 1);

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
        playerId = message.PlayerId;

        clientInfos.Add(new RemoteClientInfo
        {
            PlayerId = message.PlayerId
        });
    }
    private void HandleSpawnPlayerMessage(SpawnPlayerMessage message)
    {
        var isSpawningMe = (message.PlayerId == playerId);
        var clientInfo = clientInfos.FirstOrDefault(ci => ci.PlayerId == message.PlayerId);

        if(clientInfo == null)
        {
            clientInfo = new RemoteClientInfo
            {
                PlayerId = message.PlayerId
            };

            clientInfos.Add(clientInfo);
        }

        var playerObject = isServerRemote
            ? OsFps.Instance.SpawnLocalPlayer(clientInfo)
            : OsFps.Instance.FindPlayerObject(clientInfo.PlayerId);

        if (isSpawningMe)
        {
            AttachCameraToPlayer(clientInfo.PlayerId);
        }
    }
    private void HandlePlayerInputMessage(PlayerInputMessage message)
    {
        Debug.Log("Client Received Player Input Message");
    }

    private void SendPlayerInput()
    {
        var playerObject = OsFps.Instance.FindPlayerObject(playerId);
        var playerComponent = (playerObject != null) ? playerObject.GetComponent<PlayerComponent>() : null;
        if (playerComponent == null) return;

        var message = new PlayerInputMessage
        {
            PlayerId = playerId,
            PlayerInput = playerComponent.CurrentInput
        };

        ClientPeer.SendMessageToServer(ClientPeer.unreliableStateUpdateChannelId, message.Serialize());
    }
}