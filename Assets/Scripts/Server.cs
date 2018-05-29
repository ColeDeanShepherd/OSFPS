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

        // Send out a chat message.
        OsFps.Instance.CallRpcOnAllClients("ClientOnReceiveChatMessage", reliableSequencedChannelId, new
        {
            playerId = (uint?)null,
            message = $"{playerId} joined."
        });
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

        // Send out a chat message.
        OsFps.Instance.CallRpcOnAllClients("ClientOnReceiveChatMessage", reliableSequencedChannelId, new
        {
            playerId = (uint?)null,
            message = $"{playerId} left."
        });
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

    [Rpc(ExecuteOn = NetworkPeerType.Server)]
    public void ServerOnPlayerReloadPressed(uint playerId)
    {
        // TODO: Make sure the player ID is correct.
        var playerObjectComponent = OsFps.Instance.FindPlayerObjectComponent(playerId);

        if (playerObjectComponent.State.CanReload)
        {
            PlayerSystem.Instance.ServerPlayerStartReload(playerObjectComponent);
        }

        // TODO: Send to all other players???
    }

    [Rpc(ExecuteOn = NetworkPeerType.Server)]
    public void ServerOnPlayerTriggerPulled(uint playerId)
    {
        // TODO: Make sure the player ID is correct.
        var playerObjectComponent = OsFps.Instance.FindPlayerObjectComponent(playerId);
        PlayerSystem.Instance.ServerPlayerPullTrigger(this, playerObjectComponent);

        OsFps.Instance.CallRpcOnAllClients("ClientOnTriggerPulled", reliableSequencedChannelId, new
        {
            playerId
        });
    }

    [Rpc(ExecuteOn = NetworkPeerType.Server)]
    public void ServerOnPlayerThrowGrenade(uint playerId)
    {
        // TODO: Make sure the player ID is correct.
        var playerObjectComponent = OsFps.Instance.FindPlayerObjectComponent(playerId);
        GrenadeSystem.Instance.ServerPlayerThrowGrenade(this, playerObjectComponent);
    }
    
    [Rpc(ExecuteOn = NetworkPeerType.Server)]
    public void ServerOnPlayerSwitchGrenadeType(uint playerId)
    {
        var playerObjectComponent = OsFps.Instance.FindPlayerObjectComponent(playerId);
        if (playerObjectComponent == null) return;

        var grenadeSlots = playerObjectComponent.State.GrenadeSlots;

        for (var iOffset = 1; iOffset < grenadeSlots.Length; iOffset++)
        {
            var grenadeSlotIndex = MathfExtensions.Wrap(
                playerObjectComponent.State.CurrentGrenadeSlotIndex + iOffset,
                0,
                grenadeSlots.Length - 1
            );
            var grenadeSlot = grenadeSlots[grenadeSlotIndex];

            if (grenadeSlot.GrenadeCount > 0)
            {
                playerObjectComponent.State.CurrentGrenadeSlotIndex = (byte)grenadeSlotIndex;
            }
        }
    }

    [Rpc(ExecuteOn = NetworkPeerType.Server)]
    public void ServerOnChatMessage(uint? playerId, string message)
    {
        OsFps.Instance.CallRpcOnAllClients("ClientOnReceiveChatMessage", reliableSequencedChannelId, new
        {
            playerId,
            message
        });
    }

    [Rpc(ExecuteOn = NetworkPeerType.Server)]
    public void ServerOnChangeWeapon(uint playerId, byte weaponIndex)
    {
        OsFps.Instance.CallRpcOnAllClients("ClientOnChangeWeapon", reliableSequencedChannelId, new
        {
            playerId = playerId,
            weaponIndex = weaponIndex
        });

        var playerObjectComponent = OsFps.Instance.FindPlayerObjectComponent(playerId);

        if (playerObjectComponent == null) return;

        playerObjectComponent.State.CurrentWeaponIndex = weaponIndex;
        playerObjectComponent.State.ReloadTimeLeft = -1;
    }

    [Rpc(ExecuteOn = NetworkPeerType.Server)]
    public void ServerOnReceivePlayerInput(uint playerId, PlayerInput playerInput, Vector2 lookDirAngles)
    {
        // TODO: Make sure the player ID is correct.
        var playerObjectComponent = OsFps.Instance.FindPlayerObjectComponent(playerId);
        if (playerObjectComponent == null) return;

        var playerObjectState = playerObjectComponent.State;
        playerObjectState.Input = playerInput;
        playerObjectState.LookDirAngles = lookDirAngles;
    }
    
    [Rpc(ExecuteOn = NetworkPeerType.Server)]
    public void ServerOnPlayerTryJump(uint playerId)
    {
        var playerObjectComponent = OsFps.Instance.FindPlayerObjectComponent(playerId);
        if (playerObjectComponent == null) return;

        if (OsFps.Instance.IsPlayerGrounded(playerObjectComponent))
        {
            PlayerSystem.Instance.Jump(playerObjectComponent);
        }
    }
    #endregion
}