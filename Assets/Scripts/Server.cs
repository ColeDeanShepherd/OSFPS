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

    public GameStateScraperSystem gameStateScraperSystem = new GameStateScraperSystem();

    public void Start()
    {
        playerIdsByConnectionId = new Dictionary<int, uint>();

        ServerPeer = new ServerPeer();
        ServerPeer.OnClientConnected += OnClientConnected;
        ServerPeer.OnClientDisconnected += OnClientDisconnected;
        ServerPeer.OnReceiveDataFromClient += OnReceiveDataFromClient;

        var connectionConfig = OsFps.Instance.CreateConnectionConfig(
            out reliableSequencedChannelId,
            out reliableChannelId,
            out unreliableStateUpdateChannelId
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
    public void LateUpdate()
    {
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

        // create player data object
        var playerState = new PlayerState
        {
            Id = playerId,
            Kills = 0,
            Deaths = 0
        };
        OsFps.Instance.CreateLocalPlayerDataObject(playerState);

        // Let the client know its player ID.
        OsFps.Instance.CallRpcOnClient("ClientOnSetPlayerId", connectionId, reliableSequencedChannelId, new
        {
            playerId = playerId
        });

        // Spawn the player.
        PlayerSystem.Instance.ServerSpawnPlayer(this, playerId);
    }
    public void OnClientDisconnected(int connectionId)
    {
        var playerId = playerIdsByConnectionId[connectionId];
        playerIdsByConnectionId.Remove(connectionId);

        var playerObject = OsFps.Instance.FindPlayerObject(playerId);
        if (playerObject != null)
        {
            Object.Destroy(playerObject);
        }

        var playerComponent = OsFps.Instance.FindPlayerComponent(playerId);
        Object.Destroy(playerComponent.gameObject);
    }

    public void SendMessageToAllClients(int channelId, byte[] serializedMessage)
    {
        var connectionIds = playerIdsByConnectionId.Keys.Select(x => x).ToList();

        foreach (var connectionId in connectionIds)
        {
            SendMessageToClientHandleErrors(connectionId, channelId, serializedMessage);
        }
    }
    public void SendMessageToClient(int connectionId, int channelId, byte[] serializedMessage)
    {
        SendMessageToClientHandleErrors(connectionId, channelId, serializedMessage);
    }
    
    public int reliableSequencedChannelId;
    public int reliableChannelId;
    public int unreliableStateUpdateChannelId;
    private Dictionary<int, uint> playerIdsByConnectionId;
    private ThrottledAction SendGameStatePeriodicFunction;
    
    private void OnMapLoaded(Scene scene, LoadSceneMode loadSceneMode)
    {
        SceneManager.sceneLoaded -= OnMapLoaded;
        
        SendGameStatePeriodicFunction = new ThrottledAction(SendGameState, 1.0f / 30);

        gameStateScraperSystem.ServerInitWeaponObjectStatesInGameObjects(this);
        gameStateScraperSystem.ServerInitWeaponSpawnerStatesInGameObjects(this);

        OnServerStarted?.Invoke();
    }
    
    private void SendGameState()
    {
        Debug.Log("Sending game state...");

        OsFps.Instance.CallRpcOnAllClients("ClientOnReceiveGameState", unreliableStateUpdateChannelId, new
        {
            gameState = gameStateScraperSystem.GetGameState()
        });
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
    public uint GenerateNetworkId()
    {
        var netId = _nextNetworkId;
        _nextNetworkId++;
        return netId;
    }

    public PositionOrientation3d GetNextSpawnPoint()
    {
        var spawnPointObjects = GameObject.FindGameObjectsWithTag(OsFps.SpawnPointTag);

        if (spawnPointObjects.Length > 0)
        {
            var spawnPointObject = spawnPointObjects[Random.Range(0, spawnPointObjects.Length)];

            return new PositionOrientation3d
            {
                Position = spawnPointObject.transform.position,
                Orientation = spawnPointObject.transform.rotation
            };
        }
        else
        {
            return new PositionOrientation3d
            {
                Position = Vector3.zero,
                Orientation = Quaternion.identity
            };
        }
    }
    
    #region Message Handlers
    private void OnReceiveDataFromClient(int connectionId, int channelId, byte[] bytesReceived)
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
}