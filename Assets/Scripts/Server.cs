using UnityEngine;
using NetLib;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.Linq;

public class Server
{
    public const int PortNumber = 32321;
    public const int MaxPlayerCount = 4;

    public delegate void ServerStartedHandler();
    public event ServerStartedHandler OnServerStarted;

    public ServerPeer serverPeer;

    public void Start()
    {
        serverPeer = new ServerPeer();
        serverPeer.OnClientConnected += OnClientConnected;

        clientInfos = new List<RemoteClientInfo>();
        serverPeer.Start(PortNumber, MaxPlayerCount);

        SceneManager.sceneLoaded += OnMapLoaded;
        SceneManager.LoadScene("Test Map");
    }
    public void Stop()
    {
        serverPeer.Stop();
    }
    public void Update()
    {
        serverPeer.ReceiveAndHandleNetworkEvents();
    }
    public void OnClientConnected(int connectionId)
    {
        var playerId = GenerateNetworkId();

        // Store information about the client.
        var clientInfo = new RemoteClientInfo
        {
            ConnectionId = connectionId,
            PlayerId = playerId
        };
        clientInfos.Add(clientInfo);

        // Let the client know it's player ID.
        var setPlayerIdMessage = new SetPlayerIdMessage
        {
            PlayerId = playerId
        };
        SendClientMessage(connectionId, serverPeer.reliableSequencedChannelId, setPlayerIdMessage);

        // Spawn the player.
        clientInfo.GameObject = SpawnPlayer(playerId);
    }

    public void SendMessageToAllClients(int channelId, NetworkMessage message)
    {
        var serializedMessage = message.Serialize();
        var connectionIds = clientInfos.Select(rci => rci.ConnectionId);

        foreach(var connectionId in connectionIds)
        {
            serverPeer.SendMessageToClient(connectionId, channelId, serializedMessage);
        }
    }
    public void SendClientMessage(int connectionId, int channelId, NetworkMessage message)
    {
        serverPeer.SendMessageToClient(connectionId, channelId, message.Serialize());
    }

    private List<RemoteClientInfo> clientInfos;

    private GameObject SpawnPlayer(uint playerId)
    {
        var playerObject = OsFps.Instance.SpawnLocalPlayer(clientInfos.First(ci => ci.PlayerId == playerId));

        var spawnPlayerMessage = new SpawnPlayerMessage
        {
            PlayerId = playerId
        };
        SendMessageToAllClients(serverPeer.reliableSequencedChannelId, spawnPlayerMessage);

        return playerObject;
    }

    private void OnMapLoaded(Scene scene, LoadSceneMode loadSceneMode)
    {
        SceneManager.sceneLoaded -= OnMapLoaded;

        if(OnServerStarted != null)
        {
            OnServerStarted();
        }
    }

    private uint _nextNetworkId = 1;
    private uint GenerateNetworkId()
    {
        var netId = _nextNetworkId;
        _nextNetworkId++;
        return netId;
    }
}