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
    public const int MaxPlayerCount = 8;
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
        ServerPeer.ShouldSendStateSnapshots = false;

        ServerPeer.Start(PortNumber, MaxPlayerCount, this, SendGameStateInterval);

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
        ServerPeer.LateUpdate();
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

        playerIdsByConnectionId.Remove(connectionId);

        // Send out a chat message.
        ServerPeer.CallRpcOnAllClients("ClientOnReceiveChatMessage", ServerPeer.reliableSequencedChannelId, new
        {
            playerId = (uint?)null,
            message = $"{playerName} left."
        });
    }
    
    private Dictionary<int, uint> playerIdsByConnectionId;
    
    private void OnMapLoaded(Scene scene, LoadSceneMode loadSceneMode)
    {
        SceneManager.sceneLoaded -= OnMapLoaded;

        var camera = Object.Instantiate(OsFps.Instance.CameraPrefab);
        var uiCullingMask = 1 << 5;
        camera.GetComponent<Camera>().cullingMask = uiCullingMask;

        var dedicatedServerScreenComponent = Object.Instantiate(
            OsFps.Instance.DedicatedServerScreenPrefab, OsFps.Instance.CanvasObject.transform
        ).GetComponent<DedicatedServerScreenComponent>();
        OsFps.Instance.MenuStack.Push(dedicatedServerScreenComponent);

        ServerPeer.ShouldSendStateSnapshots = true;
        OnServerStarted?.Invoke();
    }
    
    private uint _nextNetworkId = 1;
    public uint GenerateNetworkId()
    {
        var netId = _nextNetworkId;
        _nextNetworkId++;
        return netId;
    }
    
    public int? GetConnectionIdByPlayerId(uint playerId)
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
    [Rpc(ExecuteOn = NetworkLibrary.NetworkPeerType.Server)]
    public void ServerOnReceivePlayerInfo(string playerName)
    {
        var connectionId = ServerPeer.CurrentRpcSenderConnectionId;
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
        ServerPeer.CallRpcOnClient("ClientOnSetPlayerId", connectionId, ServerPeer.reliableSequencedChannelId, new
        {
            playerId = playerId
        });

        // Spawn the player.
        PlayerRespawnSystem.Instance.ServerSpawnPlayer(this, playerId);

        // Send out a chat message.
        ServerPeer.CallRpcOnAllClientsExcept("ClientOnReceiveChatMessage", connectionId, ServerPeer.reliableSequencedChannelId, new
        {
            playerId = (uint?)null,
            message = $"{playerName} joined."
        });
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
        var secondsToRewind = 50 * (ServerPeer.GetRoundTripTimeToClientInSeconds(connectionId.Value) ?? 0);
        PlayerObjectSystem.Instance.ServerShoot(this, playerObjectComponent, shotRay, secondsToRewind);

        ServerPeer.CallRpcOnAllClientsExcept("ClientOnTriggerPulled", connectionId.Value, ServerPeer.reliableSequencedChannelId, new
        {
            playerId,
            shotRay
        });

        if (OsFps.ShowHitScanShotsOnServer)
        {
            var serverShotRay = PlayerObjectSystem.Instance.GetShotRay(playerObjectComponent);
            WeaponSystem.Instance.CreateHitScanShotDebugLine(serverShotRay, OsFps.Instance.ClientShotRayMaterial);
        }
    }

    [Rpc(ExecuteOn = NetworkLibrary.NetworkPeerType.Server)]
    public void ServerOnPlayerThrowGrenade(uint playerId)
    {
        // TODO: Make sure the player ID is correct.
        var playerObjectComponent = PlayerObjectSystem.Instance.FindPlayerObjectComponent(playerId);
        PlayerObjectSystem.Instance.ServerPlayerThrowGrenade(this, playerObjectComponent);
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
            var playersClosestWeaponInfo = WeaponSystem.Instance.ClosestWeaponInfoByPlayerId
                .GetValueOrDefault(playerId);

            if (playersClosestWeaponInfo != null)
            {
                var closestWeaponId = playersClosestWeaponInfo.Item1;

                if (weaponId == closestWeaponId)
                {
                    var weaponComponent = WeaponSystem.Instance.FindWeaponComponent(weaponId);

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
        var rpcChannelId = ServerPeer.reliableSequencedChannelId;
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
            "ClientOnChangeWeapon", connectionId.Value, ServerPeer.reliableSequencedChannelId, rpcArgs
        );

        var playerObjectComponent = PlayerObjectSystem.Instance.FindPlayerObjectComponent(playerId);

        if (playerObjectComponent == null) return;

        playerObjectComponent.State.CurrentWeaponIndex = weaponIndex;
        playerObjectComponent.State.ReloadTimeLeft = -1;

        var currentWeapon = playerObjectComponent.State.CurrentWeapon;
        if (currentWeapon != null)
        {
            currentWeapon.TimeSinceLastShot = currentWeapon.Definition.ShotInterval;
            playerObjectComponent.State.RecoilTimeLeft = currentWeapon.Definition.RecoilTime;
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