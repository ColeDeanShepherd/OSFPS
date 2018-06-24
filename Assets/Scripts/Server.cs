using UnityEngine;
using NetworkLibrary;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEngine.Networking;

public class Server
{
    public const int PortNumber = 32321;
    public const int MaxPlayerCount = 16;
    public const float SendGameStateInterval = 1.0f / 30;

    public delegate void ServerStartedHandler();
    public event ServerStartedHandler OnServerStarted;

    public ServerPeer ServerPeer;

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
            out unreliableStateUpdateChannelId,
            out unreliableFragmentedChannelId,
            out unreliableChannelId
        );
        var hostTopology = new HostTopology(connectionConfig, MaxPlayerCount);
        ServerPeer.Start(PortNumber, hostTopology);

        SceneManager.sceneLoaded += OnMapLoaded;
        SceneManager.LoadScene(OsFps.SmallMapSceneName);
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

        PlayerSystem.Instance.ServerOnLateUpdate(this);
    }

    public void OnClientConnected(int connectionId)
    {
    }
    public void OnClientDisconnected(int connectionId)
    {
        var playerId = playerIdsByConnectionId[connectionId];
        playerIdsByConnectionId.Remove(connectionId);

        var playerObject = PlayerSystem.Instance.FindPlayerObject(playerId);
        if (playerObject != null)
        {
            Object.Destroy(playerObject);
        }

        var playerComponent = PlayerSystem.Instance.FindPlayerComponent(playerId);
        Object.Destroy(playerComponent.gameObject);

        // Send out a chat message.
        ServerPeer.CallRpcOnAllClients("ClientOnReceiveChatMessage", reliableSequencedChannelId, new
        {
            playerId = (uint?)null,
            message = $"{playerComponent.State.Name} left."
        });
    }
    
    public int reliableSequencedChannelId;
    public int reliableChannelId;
    public int unreliableStateUpdateChannelId;
    public int unreliableFragmentedChannelId;
    public int unreliableChannelId;

    private Dictionary<int, uint> playerIdsByConnectionId;
    private ThrottledAction SendGameStatePeriodicFunction;
    
    private void OnMapLoaded(Scene scene, LoadSceneMode loadSceneMode)
    {
        SceneManager.sceneLoaded -= OnMapLoaded;
        
        SendGameStatePeriodicFunction = new ThrottledAction(SendGameState, SendGameStateInterval);

        OnServerStarted?.Invoke();
    }

    private const int maxCachedSentGameStates = 60;
    //private List<GameState> cachedSentGameStates = new List<GameState>();
    private Dictionary<uint, uint> _latestAcknowledgedGameStateSequenceNumberByPlayerId = new Dictionary<uint, uint>();
    private void SendGameState()
    {
        foreach (var playerId in playerIdsByConnectionId.Values)
        {
            var playersLatestAcknowledgedGameStateSequenceNumber = _latestAcknowledgedGameStateSequenceNumberByPlayerId
                .GetValueOrDefault(playerId);

            SendGameStateDiff(playerId, GenerateSequenceNumber());
        }

        /*
        cachedSentGameStates.Add(gameState);
        if (cachedSentGameStates.Count > maxCachedSentGameStates)
        {
            cachedSentGameStates.RemoveAt(0);
        }*/
    }

    private uint _nextSequenceNumber = 1;
    public uint GenerateSequenceNumber()
    {
        var generatedSequenceNumber = _nextSequenceNumber;
        _nextSequenceNumber++;
        return generatedSequenceNumber;
    }

    private void SendGameStateDiff(uint playerId, uint sequenceNumber)
    {
        var connectionId = GetConnectionIdByPlayerId(playerId);

        byte[] messageBytes;

        using (var memoryStream = new MemoryStream())
        {
            using (var writer = new BinaryWriter(memoryStream))
            {
                writer.Write(OsFps.StateSynchronizationMessageId);
                writer.Write(sequenceNumber);
                NetworkSerializationUtils.SerializeSynchronizedComponents(
                    writer, NetLib.synchronizedComponentInfos
                );
            }

            messageBytes = memoryStream.ToArray();
        }

        ServerPeer.SendMessageToClient(connectionId.Value, unreliableFragmentedChannelId, messageBytes);
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

    private int? GetConnectionIdByPlayerId(uint playerId)
    {
        foreach (var connectionIdPlayerIdPair in playerIdsByConnectionId)
        {
            var connectionId = connectionIdPlayerIdPair.Key;
            var currentPlayerId = connectionIdPlayerIdPair.Value;

            if (currentPlayerId == playerId)
            {
                return connectionId;
            }
        }

        return null;
    }

    #region Message Handlers
    private int TEMPORARY_HACK_CURRENT_CONNECTION_ID;
    private void OnReceiveDataFromClient(int connectionId, int channelId, byte[] bytesReceived, int numBytesReceived)
    {
        using (var memoryStream = new MemoryStream(bytesReceived, 0, numBytesReceived))
        {
            using (var reader = new BinaryReader(memoryStream))
            {
                var messageTypeAsByte = reader.ReadByte();

                RpcInfo rpcInfo;

                if (messageTypeAsByte == OsFps.StateSynchronizationMessageId)
                {
                    throw new System.NotImplementedException("Servers don't support receiving state synchronization messages.");
                }
                else if (NetLib.rpcInfoById.TryGetValue(messageTypeAsByte, out rpcInfo))
                {
                    var rpcArguments = NetworkSerializationUtils.DeserializeRpcCallArguments(rpcInfo, reader);

                    TEMPORARY_HACK_CURRENT_CONNECTION_ID = connectionId;
                    NetLib.ExecuteRpc(rpcInfo.Id, this, null, rpcArguments);
                }
                else
                {
                    throw new System.NotImplementedException("Unknown message type: " + messageTypeAsByte);
                }
            }
        }
    }

    [Rpc(ExecuteOn = NetworkLibrary.NetworkPeerType.Server)]
    public void ServerOnReceivePlayerInfo(string playerName)
    {
        var connectionId = TEMPORARY_HACK_CURRENT_CONNECTION_ID;
        var playerId = GenerateNetworkId();

        // Store information about the client.
        playerIdsByConnectionId.Add(connectionId, playerId);

        // create player data object
        var playerState = new PlayerState
        {
            Id = playerId,
            Name = playerName,
            Kills = 0,
            Deaths = 0
        };
        PlayerSystem.Instance.CreateLocalPlayerDataObject(playerState);

        // Let the client know its player ID.
        ServerPeer.CallRpcOnClient("ClientOnSetPlayerId", connectionId, reliableSequencedChannelId, new
        {
            playerId = playerId
        });

        // Spawn the player.
        PlayerRespawnSystem.Instance.ServerSpawnPlayer(this, playerId);

        // Send out a chat message.
        ServerPeer.CallRpcOnAllClientsExcept("ClientOnReceiveChatMessage", connectionId, reliableSequencedChannelId, new
        {
            playerId = (uint?)null,
            message = $"{playerName} joined."
        });
    }

    [Rpc(ExecuteOn = NetworkLibrary.NetworkPeerType.Server)]
    public void ServerOnReceiveClientGameStateAck(uint playerId, uint gameStateSequenceNumber)
    {
        var latestSequenceNumber =
            _latestAcknowledgedGameStateSequenceNumberByPlayerId.GetValueOrDefault(playerId);

        if (gameStateSequenceNumber > latestSequenceNumber)
        {
            _latestAcknowledgedGameStateSequenceNumberByPlayerId[playerId] = gameStateSequenceNumber;
        }
    }

    [Rpc(ExecuteOn = NetworkLibrary.NetworkPeerType.Server)]
    public void ServerOnPlayerReloadPressed(uint playerId)
    {
        // TODO: Make sure the player ID is correct.
        var playerObjectComponent = PlayerSystem.Instance.FindPlayerObjectComponent(playerId);

        if (playerObjectComponent.State.CanReload)
        {
            PlayerSystem.Instance.ServerPlayerStartReload(playerObjectComponent);
        }

        // TODO: Send to all other players???
    }

    [Rpc(ExecuteOn = NetworkLibrary.NetworkPeerType.Server)]
    public void ServerOnPlayerTriggerPulled(uint playerId, Ray shotRay)
    {
        // TODO: Make sure the player ID is correct.
        var playerObjectComponent = PlayerSystem.Instance.FindPlayerObjectComponent(playerId);
        var connectionId = GetConnectionIdByPlayerId(playerId);
        var secondsToRewind = 50 * (ServerPeer.GetRoundTripTimeToClient(connectionId.Value) ?? 0);
        PlayerSystem.Instance.ServerShoot(this, playerObjectComponent, shotRay, secondsToRewind);

        ServerPeer.CallRpcOnAllClientsExcept("ClientOnTriggerPulled", connectionId.Value, reliableSequencedChannelId, new
        {
            playerId,
            shotRay
        });

        if (OsFps.ShowHitScanShotsOnServer)
        {
            var serverShotRay = PlayerSystem.Instance.GetShotRay(playerObjectComponent);
            OsFps.Instance.CreateHitScanShotDebugLine(serverShotRay, OsFps.Instance.ClientShotRayMaterial);
        }
    }

    [Rpc(ExecuteOn = NetworkLibrary.NetworkPeerType.Server)]
    public void ServerOnPlayerThrowGrenade(uint playerId)
    {
        // TODO: Make sure the player ID is correct.
        var playerObjectComponent = PlayerSystem.Instance.FindPlayerObjectComponent(playerId);
        GrenadeSystem.Instance.ServerPlayerThrowGrenade(this, playerObjectComponent);
    }
    
    [Rpc(ExecuteOn = NetworkLibrary.NetworkPeerType.Server)]
    public void ServerOnPlayerSwitchGrenadeType(uint playerId)
    {
        var playerObjectComponent = PlayerSystem.Instance.FindPlayerObjectComponent(playerId);
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

    [Rpc(ExecuteOn = NetworkLibrary.NetworkPeerType.Server)]
    public void ServerOnPlayerTryPickupWeapon(uint playerId, uint weaponId)
    {
        var playerObjectComponent = PlayerSystem.Instance.FindPlayerObjectComponent(playerId);

        if (playerObjectComponent != null)
        {
            var playersClosestWeaponInfo = WeaponObjectSystem.Instance.ClosestWeaponInfoByPlayerId
                .GetValueOrDefault(playerId);

            if (playersClosestWeaponInfo != null)
            {
                var closestWeaponId = playersClosestWeaponInfo.Item1;

                if (weaponId == closestWeaponId)
                {
                    var weaponComponent = WeaponSystem.Instance.FindWeaponComponent(weaponId);
                    PlayerSystem.Instance.ServerPlayerTryToPickupWeapon(this, playerObjectComponent, weaponComponent);
                }
            }
        }
    }

    [Rpc(ExecuteOn = NetworkLibrary.NetworkPeerType.Server)]
    public void ServerOnChatMessage(uint? playerId, string message)
    {
        var rpcChannelId = reliableSequencedChannelId;
        var rpcArgs = new
        {
            playerId,
            message
        };
        ServerPeer.CallRpcOnAllClients("ClientOnReceiveChatMessage", rpcChannelId, rpcArgs);
    }

    [Rpc(ExecuteOn = NetworkLibrary.NetworkPeerType.Server)]
    public void ServerOnChangeWeapon(uint playerId, byte weaponIndex)
    {
        var connectionId = GetConnectionIdByPlayerId(playerId);
        var rpcArgs = new
        {
            playerId = playerId,
            weaponIndex = weaponIndex
        };
        ServerPeer.CallRpcOnAllClientsExcept(
            "ClientOnChangeWeapon", connectionId.Value, reliableSequencedChannelId, rpcArgs
        );

        var playerObjectComponent = PlayerSystem.Instance.FindPlayerObjectComponent(playerId);

        if (playerObjectComponent == null) return;

        playerObjectComponent.State.CurrentWeaponIndex = weaponIndex;
        playerObjectComponent.State.ReloadTimeLeft = -1;

        var currentWeapon = playerObjectComponent.State.CurrentWeapon;
        if (currentWeapon != null)
        {
            currentWeapon.TimeSinceLastShot = currentWeapon.Definition.ShotInterval;
        }
    }

    [Rpc(ExecuteOn = NetworkLibrary.NetworkPeerType.Server)]
    public void ServerOnReceivePlayerInput(uint playerId, PlayerInput playerInput, Vector2 lookDirAngles)
    {
        // TODO: Make sure the player ID is correct.
        var playerObjectComponent = PlayerSystem.Instance.FindPlayerObjectComponent(playerId);
        if (playerObjectComponent == null) return;

        var playerObjectState = playerObjectComponent.State;
        playerObjectState.Input = playerInput;
        playerObjectState.LookDirAngles = lookDirAngles;
    }
    
    [Rpc(ExecuteOn = NetworkLibrary.NetworkPeerType.Server)]
    public void ServerOnPlayerTryJump(uint playerId)
    {
        var playerObjectComponent = PlayerSystem.Instance.FindPlayerObjectComponent(playerId);
        if (playerObjectComponent == null) return;

        if (PlayerSystem.Instance.IsPlayerGrounded(playerObjectComponent))
        {
            PlayerSystem.Instance.Jump(playerObjectComponent);
        }
    }
    #endregion
}