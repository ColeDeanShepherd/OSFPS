using UnityEngine;
using NetworkLibrary;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEngine.Networking;
using Newtonsoft.Json;
using Unity.Mathematics;

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
        ServerPeer.Update();
    }
    public void LateUpdate()
    {
        if (SendGameStatePeriodicFunction != null)
        {
            SendGameStatePeriodicFunction.TryToCall();
        }
    }
    public void OnGui()
    {
        DrawNetworkStats();
    }
    private void DrawNetworkStats()
    {
        var connectionId = ServerPeer.connectionIds.FirstOrDefault();
        var networkStats = ServerPeer.GetNetworkStats((connectionId > 0) ? connectionId : (int?)null);
        var position = new Vector2(30, 30);
        GUI.Label(new Rect(position, new Vector2(800, 800)), JsonConvert.SerializeObject(networkStats));
    }

    public void OnClientConnected(int connectionId)
    {
    }
    public void OnClientDisconnected(int connectionId)
    {
        var playerId = playerIdsByConnectionId[connectionId];

        var playerObject = PlayerObjectSystem.Instance.FindPlayerObject(playerId);
        if (playerObject != null)
        {
            Object.Destroy(playerObject);
        }

        var playerComponent = PlayerObjectSystem.Instance.FindPlayerComponent(playerId);
        var playerName = playerComponent.State.Name;
        Object.Destroy(playerComponent.gameObject);

        networkedGameStateCache.OnPlayerDisconnected(playerId);

        playerIdsByConnectionId.Remove(connectionId);

        // Send out a chat message.
        ServerPeer.CallRpcOnAllClients("ClientOnReceiveChatMessage", reliableSequencedChannelId, new
        {
            playerId = (uint?)null,
            message = $"{playerName} left."
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

        var camera = Object.Instantiate(OsFps.Instance.CameraPrefab);
        camera.GetComponent<Camera>().cullingMask = 1 << 5;

        OnServerStarted?.Invoke();
    }

    private const int maxCachedSentGameStates = 100;
    private NetworkedGameStateCache networkedGameStateCache = new NetworkedGameStateCache(maxCachedSentGameStates);
    private void SendGameState()
    {
        // Get the current game state.
        var currentGameState = NetLib.GetCurrentNetworkedGameState(generateSequenceNumber: true);

        // Send the game state deltas.
        foreach (var playerId in playerIdsByConnectionId.Values)
        {
            var oldGameState = networkedGameStateCache.GetNetworkedGameStateToDiffAgainst(playerId);
            SendGameStateDiff(playerId, currentGameState, oldGameState);
        }

        // Cache the game state for future deltas.
        networkedGameStateCache.AddGameState(currentGameState);
    }

    private void SendGameStateDiff(uint playerId, NetworkedGameState gameState, NetworkedGameState oldGameState)
    {
        byte[] messageBytes;

        using (var memoryStream = new MemoryStream())
        {
            using (var writer = new BinaryWriter(memoryStream))
            {
                writer.Write(OsFps.StateSynchronizationMessageId);
                writer.Write(gameState.SequenceNumber);
                writer.Write(oldGameState.SequenceNumber);
                NetworkSerializationUtils.SerializeNetworkedGameState(writer, gameState, oldGameState);
            }

            messageBytes = memoryStream.ToArray();
        }
        
        var connectionId = GetConnectionIdByPlayerId(playerId);
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
        if (spawnPointObjects.Length == 0)
        {
            OsFps.Logger.LogWarning("No spawn points.");

            return new PositionOrientation3d
            {
                Position = Vector3.zero,
                Orientation = Quaternion.identity
            };
        }

        var unobstructedSpawnPointObjects = spawnPointObjects
            .Where(spo => !IsSpawnPointObstructed(spo.transform.position))
            .ToArray();

        GameObject spawnPointObject;
        if (unobstructedSpawnPointObjects.Length > 0)
        {
            spawnPointObject = unobstructedSpawnPointObjects[Random.Range(0, unobstructedSpawnPointObjects.Length)];
        }
        else
        {
            spawnPointObject = spawnPointObjects[Random.Range(0, spawnPointObjects.Length)];
        }

        return new PositionOrientation3d
        {
            Position = spawnPointObject.transform.position,
            Orientation = spawnPointObject.transform.rotation
        };
    }
    private bool IsSpawnPointObstructed(Vector3 spawnPoint)
    {
        var sphereRadius = 0.25f;
        return Physics.CheckSphere(spawnPoint + Vector3.up, sphereRadius);
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
        PlayerObjectSystem.Instance.CreateLocalPlayerDataObject(playerState);

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
        networkedGameStateCache.AcknowledgeGameStateForPlayer(playerId, gameStateSequenceNumber);
    }

    [Rpc(ExecuteOn = NetworkLibrary.NetworkPeerType.Server)]
    public void ServerOnPlayerReloadPressed(uint playerId)
    {
        // TODO: Make sure the player ID is correct.
        var playerObjectComponent = PlayerObjectSystem.Instance.FindPlayerObjectComponent(playerId);

        if (playerObjectComponent.State.CanReload)
        {
            PlayerObjectSystem.Instance.ServerPlayerStartReload(playerObjectComponent);
        }

        // TODO: Send to all other players???
    }

    [Rpc(ExecuteOn = NetworkLibrary.NetworkPeerType.Server)]
    public void ServerOnPlayerTriggerPulled(uint playerId, Ray shotRay)
    {
        // TODO: Make sure the player ID is correct.
        var playerObjectComponent = PlayerObjectSystem.Instance.FindPlayerObjectComponent(playerId);
        var connectionId = GetConnectionIdByPlayerId(playerId);
        var secondsToRewind = 50 * (ServerPeer.GetRoundTripTimeToClient(connectionId.Value) ?? 0);
        PlayerObjectSystem.Instance.ServerShoot(this, playerObjectComponent, shotRay, secondsToRewind);

        ServerPeer.CallRpcOnAllClientsExcept("ClientOnTriggerPulled", connectionId.Value, reliableSequencedChannelId, new
        {
            playerId,
            shotRay
        });

        if (OsFps.ShowHitScanShotsOnServer)
        {
            var serverShotRay = PlayerObjectSystem.Instance.GetShotRay(playerObjectComponent);
            OsFps.Instance.CreateHitScanShotDebugLine(serverShotRay, OsFps.Instance.ClientShotRayMaterial);
        }
    }

    [Rpc(ExecuteOn = NetworkLibrary.NetworkPeerType.Server)]
    public void ServerOnPlayerThrowGrenade(uint playerId)
    {
        // TODO: Make sure the player ID is correct.
        var playerObjectComponent = PlayerObjectSystem.Instance.FindPlayerObjectComponent(playerId);
        GrenadeSystem.Instance.ServerPlayerThrowGrenade(this, playerObjectComponent);
    }
    
    [Rpc(ExecuteOn = NetworkLibrary.NetworkPeerType.Server)]
    public void ServerOnPlayerSwitchGrenadeType(uint playerId)
    {
        var playerObjectComponent = PlayerObjectSystem.Instance.FindPlayerObjectComponent(playerId);
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
        var playerObjectComponent = PlayerObjectSystem.Instance.FindPlayerObjectComponent(playerId);

        if (playerObjectComponent != null)
        {
            var playersClosestWeaponInfo = WeaponObjectSystem.Instance.ClosestWeaponInfoByPlayerId
                .GetValueOrDefault(playerId);

            if (playersClosestWeaponInfo != null)
            {
                var closestWeaponId = playersClosestWeaponInfo.Item1;

                if (weaponId == closestWeaponId)
                {
                    var weaponComponent = WeaponObjectSystem.Instance.FindWeaponComponent(weaponId);

                    if (weaponComponent != null)
                    {
                        PlayerObjectSystem.Instance.ServerPlayerTryToPickupWeapon(this, playerObjectComponent, weaponComponent);
                    }
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

        var playerObjectComponent = PlayerObjectSystem.Instance.FindPlayerObjectComponent(playerId);

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
    public void ServerOnReceivePlayerInput(uint playerId, PlayerInput playerInput, float2 lookDirAngles)
    {
        // TODO: Make sure the player ID is correct.
        var playerObjectComponent = PlayerObjectSystem.Instance.FindPlayerObjectComponent(playerId);
        if (playerObjectComponent == null) return;

        var playerObjectState = playerObjectComponent.State;
        playerObjectState.Input = playerInput;
        playerObjectState.LookDirAngles = lookDirAngles;
    }
    
    [Rpc(ExecuteOn = NetworkLibrary.NetworkPeerType.Server)]
    public void ServerOnPlayerTryJump(uint playerId)
    {
        var playerObjectComponent = PlayerObjectSystem.Instance.FindPlayerObjectComponent(playerId);
        if (playerObjectComponent == null) return;

        if (PlayerObjectSystem.Instance.IsPlayerGrounded(playerObjectComponent))
        {
            PlayerObjectSystem.Instance.Jump(playerObjectComponent);
        }
    }
    #endregion
}