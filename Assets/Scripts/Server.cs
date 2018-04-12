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
        ServerPeer.OnClientDisconnected += OnClientDisconnected;
        ServerPeer.OnReceiveDataFromClient += OnReceiveDataFromClient;

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

        UpdatePlayers();
    }
    public void LateUpdate()
    {
        UpdateGameState();

        if (SendGameStatePeriodicFunction != null)
        {
            SendGameStatePeriodicFunction.TryToCall();
        }
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
    public void OnClientDisconnected(int connectionId)
    {
        var playerId = playerIdsByConnectionId[connectionId];
        var playerStateIndex = CurrentGameState.Players.FindIndex(ps => ps.Id == playerId);

        playerIdsByConnectionId.Remove(connectionId);
        CurrentGameState.Players.RemoveAt(playerStateIndex);

        Object.Destroy(OsFps.Instance.FindPlayerObject(playerId));
    }

    public void SendMessageToAllClients(int channelId, INetworkMessage message)
    {
        var serializedMessage = NetworkSerializationUtils.SerializeWithType(message);
        var connectionIds = playerIdsByConnectionId.Keys.Select(x => x).ToList();

        foreach(var connectionId in connectionIds)
        {
            SendMessageToClientHandleErrors(connectionId, channelId, serializedMessage);
        }
    }
    public void SendMessageToClient(int connectionId, int channelId, INetworkMessage message)
    {
        var serializedMessage = NetworkSerializationUtils.SerializeWithType(message);
        SendMessageToClientHandleErrors(connectionId, channelId, serializedMessage);
    }
    
    private int reliableSequencedChannelId;
    private int unreliableStateUpdateChannelId;
    private Dictionary<int, uint> playerIdsByConnectionId;
    private ThrottledAction SendGameStatePeriodicFunction;

    private GameObject SpawnPlayer(uint playerId, Vector3 position, float yAngle)
    {
        var playerState = CurrentGameState.Players.First(ps => ps.Id == playerId);
        playerState.Position = position;
        playerState.EulerAngles = new Vector3(0, yAngle, 0);

        var playerObject = OsFps.Instance.SpawnLocalPlayer(playerState);

        var spawnPlayerMessage = new SpawnPlayerMessage
        {
            PlayerId = playerId,
            PlayerPosition = position,
            PlayerYAngle = yAngle
        };
        //SendMessageToAllClients(reliableSequencedChannelId, spawnPlayerMessage);

        return playerObject;
    }

    private void OnMapLoaded(Scene scene, LoadSceneMode loadSceneMode)
    {
        SceneManager.sceneLoaded -= OnMapLoaded;

        SendGameStatePeriodicFunction = new ThrottledAction(SendGameState, 1.0f / 30);

        if (OnServerStarted != null)
        {
            OnServerStarted();
        }
    }

    private void SendMessageToClientHandleErrors(int connectionId, int channelId, byte[] serializedMessage)
    {
        var networkError = ServerPeer.SendMessageToClient(connectionId, channelId, serializedMessage);
        
        if (networkError != NetworkError.Ok)
        {
            Debug.LogError(string.Format("Failed sending message to client. Error: {0}", networkError));
        }
    }

    private uint _nextNetworkId = 1;
    private uint GenerateNetworkId()
    {
        var netId = _nextNetworkId;
        _nextNetworkId++;
        return netId;
    }

    private void UpdatePlayers()
    {
        foreach (var playerState in CurrentGameState.Players)
        {
            OsFps.Instance.UpdatePlayer(playerState);
        }
    }
    private void UpdateGameState()
    {
        foreach(var playerState in CurrentGameState.Players)
        {
            var playerComponent = OsFps.Instance.FindPlayerComponent(playerState.Id);

            playerState.Position = playerComponent.transform.position;
            playerState.EulerAngles = OsFps.Instance.GetPlayerEulerAngles(playerComponent);
        }
    }
    private void SendGameState()
    {
        var message = new GameStateMessage
        {
            GameState = CurrentGameState
        };
        SendMessageToAllClients(unreliableStateUpdateChannelId, message);
    }

    private void OnReceiveDataFromClient(int connectionId, int channelId, byte[] bytesReceived)
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
        // TODO: Make sure the player ID is correct.
        var playerState = CurrentGameState.Players.First(ps => ps.Id == message.PlayerId);
        playerState.Input = message.PlayerInput;
        playerState.EulerAngles = message.EulerAngles;
    }
}