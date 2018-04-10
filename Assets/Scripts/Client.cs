using NetLib;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Client
{
    public ClientPeer ClientPeer;
    public GameObject Camera;

    public void Start(bool isServerRemote)
    {
        this.isServerRemote = isServerRemote;

        clientInfos = new List<RemoteClientInfo>();

        ClientPeer = new ClientPeer();
        ClientPeer.OnReceiveDataFromServer += OnReceiveDataFromServer;

        ClientPeer.Start(portNumber: null, maxConnectionCount: 1);

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
    }

    private uint playerId;
    private bool isServerRemote;
    private List<RemoteClientInfo> clientInfos;

    private void OnReceiveDataFromServer(int channelId, byte[] bytesReceived)
    {
        var reader = new BinaryReader(new MemoryStream(bytesReceived));
        var messageType = (NetworkMessageType)reader.ReadByte();

        switch(messageType)
        {
            case NetworkMessageType.SetPlayerIdMessage:
                var setPlayerIdMessage = new SetPlayerIdMessage();
                setPlayerIdMessage.Deserialize(reader);

                HandleSetPlayerIdMessage(setPlayerIdMessage);
                break;
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
        playerId = message.PlayerId;

        clientInfos.Add(new RemoteClientInfo
        {
            PlayerId = message.PlayerId
        });
    }
    private void HandleSpawnPlayerMessage(SpawnPlayerMessage message)
    {
        var isSpawningMe = (message.PlayerId == playerId);
        var clientInfo = clientInfos.First(ci => ci.PlayerId == message.PlayerId);
        var playerObject = (!isSpawningMe || isServerRemote)
            ? OsFps.Instance.SpawnLocalPlayer(clientInfo)
            : OsFps.Instance.FindPlayerObject(clientInfo.PlayerId);

        if (isSpawningMe)
        {
            Camera.transform.position = Vector3.zero;
            Camera.transform.rotation = Quaternion.identity;

            Camera.transform.SetParent(playerObject.transform);
        }
    }
}