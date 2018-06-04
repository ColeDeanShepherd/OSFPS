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
    public const int MaxPlayerCount = 16;
    public const float SendGameStateInterval = 1.0f / 30;

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
    public void OnGui()
    {
        //DrawMenu();
    }

    private void DrawMenu()
    {
        const float buttonWidth = 200;
        const float buttonHeight = 30;
        const float buttonSpacing = 10;
        const int buttonCount = 1;
        const float menuWidth = buttonWidth;
        const float menuHeight = (buttonCount * buttonHeight) + ((buttonCount - 1) * buttonSpacing);

        var buttonSize = new Vector2(buttonWidth, buttonHeight);
        var position = new Vector2(
            (Screen.width / 2) - (menuWidth / 2),
            (Screen.height / 2) - (menuHeight / 2)
        );

        if (GUI.Button(new Rect(position, buttonSize), "Exit Menu"))
        {
            OsFps.Instance.StopServer();
        }
        position.y += buttonSize.y + buttonSpacing;
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
        OsFps.Instance.CallRpcOnAllClientsExcept("ClientOnReceiveChatMessage", connectionId, reliableSequencedChannelId, new
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
        var connectionIds = playerIdsByConnectionId.Keys.Select(x => x);

        foreach (var connectionId in connectionIds)
        {
            SendMessageToClientHandleErrors(connectionId, channelId, serializedMessage);
        }
    }
    public void SendMessageToAllClientsExcept(int exceptConnectionId, int channelId, byte[] serializedMessage)
    {
        var connectionIds = playerIdsByConnectionId.Keys.Select(x => x);

        foreach (var connectionId in connectionIds)
        {
            if (connectionId == exceptConnectionId) continue;

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
    
    private void SendGameState()
    {
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
            OsFps.Logger.LogError(string.Format("Failed sending message to client. Error: {0}", networkError));
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
    public void ServerOnPlayerTriggerPulled(uint playerId, Ray shotRay)
    {
        // TODO: Make sure the player ID is correct.
        var playerObjectComponent = OsFps.Instance.FindPlayerObjectComponent(playerId);
        var connectionId = GetConnectionIdByPlayerId(playerId);
        var secondsToRewind = 50 * (ServerPeer.GetRoundTripTimeToClient(connectionId.Value) ?? 0);
        PlayerSystem.Instance.ServerShoot(this, playerObjectComponent, shotRay, secondsToRewind);

        OsFps.Instance.CallRpcOnAllClientsExcept("ClientOnTriggerPulled", connectionId.Value, reliableSequencedChannelId, new
        {
            playerId
        });

        if (OsFps.ShowHitScanShotsOnServer)
        {
            var serverShotRay = PlayerSystem.Instance.GetShotRay(playerObjectComponent);
            OsFps.Instance.CreateHitScanShotDebugLine(serverShotRay, OsFps.Instance.ClientShotRayMaterial);
        }
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
    public void ServerOnPlayerTryPickupWeapon(uint playerId, uint weaponId)
    {
        var playerObjectComponent = OsFps.Instance.FindPlayerObjectComponent(playerId);

        if (playerObjectComponent != null)
        {
            var playersClosestWeaponInfo = WeaponObjectSystem.Instance.ClosestWeaponInfoByPlayerId
                .GetValueOrDefault(playerId);

            if (playersClosestWeaponInfo != null)
            {
                var closestWeaponId = playersClosestWeaponInfo.Item1;

                if (weaponId == closestWeaponId)
                {
                    var weaponComponent = OsFps.Instance.FindWeaponComponent(weaponId);
                    PlayerSystem.Instance.ServerPlayerTryToPickupWeapon(this, playerObjectComponent, weaponComponent);
                }
            }
        }
    }

    [Rpc(ExecuteOn = NetworkPeerType.Server)]
    public void ServerOnChatMessage(uint? playerId, string message)
    {
        var rpcChannelId = reliableSequencedChannelId;
        var rpcArgs = new
        {
            playerId,
            message
        };
        OsFps.Instance.CallRpcOnAllClients("ClientOnReceiveChatMessage", rpcChannelId, rpcArgs);
    }

    [Rpc(ExecuteOn = NetworkPeerType.Server)]
    public void ServerOnChangeWeapon(uint playerId, byte weaponIndex)
    {
        var connectionId = GetConnectionIdByPlayerId(playerId);
        var rpcArgs = new
        {
            playerId = playerId,
            weaponIndex = weaponIndex
        };
        OsFps.Instance.CallRpcOnAllClientsExcept(
            "ClientOnChangeWeapon", connectionId.Value, reliableSequencedChannelId, rpcArgs
        );

        var playerObjectComponent = OsFps.Instance.FindPlayerObjectComponent(playerId);

        if (playerObjectComponent == null) return;

        playerObjectComponent.State.CurrentWeaponIndex = weaponIndex;
        playerObjectComponent.State.ReloadTimeLeft = -1;

        var currentWeapon = playerObjectComponent.State.CurrentWeapon;
        if (currentWeapon != null)
        {
            currentWeapon.TimeSinceLastShot = currentWeapon.Definition.ShotInterval;
        }
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