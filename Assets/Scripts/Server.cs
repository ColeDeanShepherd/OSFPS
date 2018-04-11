using UnityEngine;
using NetLib;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEngine.Networking;

public class Server
{
    public const int PortNumber = 32321;
    public const int MaxPlayerCount = 4;

    public delegate void ServerStartedHandler();
    public event ServerStartedHandler OnServerStarted;

    public ServerPeer ServerPeer;
    public GameState CurrentGameState;

    public void Start()
    {
        playerIdsByConnectionId = new Dictionary<int, uint>();
        CurrentGameState = new GameState();

        ServerPeer = new ServerPeer();
        ServerPeer.OnClientConnected += OnClientConnected;
        ServerPeer.OnReceiveDataFromClient += OnReceiveDataFromServer;

        var connectionConfig = OsFps.Instance.CreateConnectionConfig(
            out reliableSequencedChannelId, out unreliableStateUpdateChannelId
        );
        var hostTopology = new HostTopology(connectionConfig, MaxPlayerCount);
        ServerPeer.Start(PortNumber, hostTopology);

        SceneManager.sceneLoaded += OnMapLoaded;
        SceneManager.LoadScene("Test Map");
    }
    public void Stop()
    {
        ServerPeer.Stop();
    }
    public void Update()
    {
        ServerPeer.ReceiveAndHandleNetworkEvents();
    }
    public void OnClientConnected(int connectionId)
    {
        var playerId = GenerateNetworkId();

        // Store information about the client.
        playerIdsByConnectionId.Add(connectionId, playerId);
        
        var playerState = new PlayerState
        {
            Id = playerId
        };
        CurrentGameState.Players.Add(playerState);

        // Let the client know its player ID.
        var setPlayerIdMessage = new SetPlayerIdMessage
        {
            PlayerId = playerId
        };
        SendMessageToClient(connectionId, reliableSequencedChannelId, setPlayerIdMessage);

        // Spawn the player.
        SpawnPlayer(playerId, Vector3.up, 0);
    }

    public void SendMessageToAllClients(int channelId, INetworkMessage message)
    {
        var serializedMessage = NetworkSerializationUtils.SerializeWithType(message);
        var connectionIds = playerIdsByConnectionId.Keys.Select(x => x);

        foreach(var connectionId in connectionIds)
        {
            ServerPeer.SendMessageToClient(connectionId, channelId, serializedMessage);
        }
    }
    public void SendMessageToClient(int connectionId, int channelId, INetworkMessage message)
    {
        var serializedMessage = NetworkSerializationUtils.SerializeWithType(message);
        ServerPeer.SendMessageToClient(connectionId, channelId, serializedMessage);
    }
    
    private int reliableSequencedChannelId;
    private int unreliableStateUpdateChannelId;
    private Dictionary<int, uint> playerIdsByConnectionId;

    private GameObject SpawnPlayer(uint playerId, Vector3 position, float yAngle)
    {
        var playerState = CurrentGameState.Players.First(ps => ps.Id == playerId);
        var playerObject = OsFps.Instance.SpawnLocalPlayer(playerState, position, yAngle);

        var spawnPlayerMessage = new SpawnPlayerMessage
        {
            PlayerId = playerId,
            PlayerPosition = position,
            PlayerYAngle = yAngle
        };
        SendMessageToAllClients(reliableSequencedChannelId, spawnPlayerMessage);

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

    private void OnReceiveDataFromServer(int connectionId, int channelId, byte[] bytesReceived)
    {
        var reader = new BinaryReader(new MemoryStream(bytesReceived));
        var messageType = (NetworkMessageType)reader.ReadByte();

        switch (messageType)
        {
            case NetworkMessageType.PlayerInput:
                var playerInputMessage = new PlayerInputMessage();
                playerInputMessage.Deserialize(reader);

                HandlePlayerInputMessage(playerInputMessage);
                break;
            default:
                throw new System.NotImplementedException("Unknown message type: " + messageType);
        }
    }
    private void HandlePlayerInputMessage(PlayerInputMessage message)
    {
        if (message.PlayerId != OsFps.Instance.CurrentPlayerId)
        {
            OsFps.Instance.FindPlayerObject(message.PlayerId)
                .GetComponent<PlayerComponent>()
                .CurrentInput = message.PlayerInput;
        }

        SendMessageToAllClients(unreliableStateUpdateChannelId, message);
    }
}