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
        var setPlayerIdMessage = new SetPlayerIdMessage
        {
            PlayerId = playerId
        };
        SendMessageToClient(connectionId, reliableSequencedChannelId, setPlayerIdMessage);

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

        var message = new GameStateMessage
        {
            GameState = gameStateScraperSystem.GetGameState()
        };
        SendMessageToAllClients(unreliableStateUpdateChannelId, message);
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
        var messageType = (NetworkMessageType)reader.ReadByte();

        switch (messageType)
        {
            case NetworkMessageType.PlayerInput:
                var playerInputMessage = new PlayerInputMessage();
                playerInputMessage.Deserialize(reader);

                HandlePlayerInputMessage(playerInputMessage);
                break;
            case NetworkMessageType.TriggerPulled:
                var triggerPulledMessage = new TriggerPulledMessage();
                triggerPulledMessage.Deserialize(reader);

                HandleTriggerPulledMessage(triggerPulledMessage);
                break;
            case NetworkMessageType.ReloadPressed:
                var reloadPressedMessage = new ReloadPressedMessage();
                reloadPressedMessage.Deserialize(reader);

                HandleReloadPressedMessage(reloadPressedMessage);
                break;
            case NetworkMessageType.ThrowGrenade:
                var throwGrenadeMessage = new ThrowGrenadeMessage();
                throwGrenadeMessage.Deserialize(reader);

                HandleThrowGrenadeMessage(throwGrenadeMessage);
                break;
            case NetworkMessageType.Chat:
                var chatMessage = new ChatMessage();
                chatMessage.Deserialize(reader);

                HandleChatMessage(chatMessage);
                break;
            case NetworkMessageType.ChangeWeapon:
                var changeWeaponMessage = new ChangeWeaponMessage();
                changeWeaponMessage.Deserialize(reader);

                HandleChangeWeaponMessage(changeWeaponMessage);
                break;
            default:
                throw new System.NotImplementedException("Unknown message type: " + messageType);
        }
    }
    private void HandlePlayerInputMessage(PlayerInputMessage message)
    {
        // TODO: Make sure the player ID is correct.
        var playerObjectComponent = OsFps.Instance.FindPlayerObjectComponent(message.PlayerId);
        if (playerObjectComponent == null) return;

        var playerObjectState = playerObjectComponent.State;
        playerObjectState.Input = message.PlayerInput;
        playerObjectState.LookDirAngles = message.LookDirAngles;
    }
    private void HandleTriggerPulledMessage(TriggerPulledMessage message)
    {
        // TODO: Make sure the player ID is correct.
        var playerObjectComponent = OsFps.Instance.FindPlayerObjectComponent(message.PlayerId);
        PlayerSystem.Instance.ServerPlayerPullTrigger(this, playerObjectComponent);

        SendMessageToAllClients(reliableSequencedChannelId, message);
    }
    private void HandleReloadPressedMessage(ReloadPressedMessage message)
    {
        // TODO: Make sure the player ID is correct.
        var playerObjectComponent = OsFps.Instance.FindPlayerObjectComponent(message.PlayerId);

        if (playerObjectComponent.State.CanReload)
        {
            PlayerSystem.Instance.ServerPlayerStartReload(playerObjectComponent);
        }

        // TODO: Send to all other players???
    }
    private void HandleThrowGrenadeMessage(ThrowGrenadeMessage message)
    {
        // TODO: Make sure the player ID is correct.
        var playerObjectComponent = OsFps.Instance.FindPlayerObjectComponent(message.PlayerId);
        GrenadeSystem.Instance.ServerPlayerThrowGrenade(this, playerObjectComponent);
    }
    private void HandleChatMessage(ChatMessage message)
    {
        SendMessageToAllClients(reliableSequencedChannelId, message);
    }
    private void HandleChangeWeaponMessage(ChangeWeaponMessage message)
    {
        SendMessageToAllClients(reliableSequencedChannelId, message);

        var playerObjectComponent = OsFps.Instance.FindPlayerObjectComponent(message.PlayerId);

        if (playerObjectComponent == null) return;

        playerObjectComponent.State.CurrentWeaponIndex = message.WeaponIndex;
        playerObjectComponent.State.ReloadTimeLeft = -1;
    }
    #endregion
}