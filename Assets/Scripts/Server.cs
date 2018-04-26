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

        UpdatePlayers();
    }
    public void LateUpdate()
    {
        UpdateGameStateFromObjects();

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
            Id = playerId,
            Kills = 0,
            Deaths = 0
        };
        CurrentGameState.Players.Add(playerState);

        // Let the client know its player ID.
        var setPlayerIdMessage = new SetPlayerIdMessage
        {
            PlayerId = playerId
        };
        SendMessageToClient(connectionId, reliableSequencedChannelId, setPlayerIdMessage);

        // Spawn the player.
        SpawnPlayer(playerState);
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
    private int reliableChannelId;
    private int unreliableStateUpdateChannelId;
    private Dictionary<int, uint> playerIdsByConnectionId;
    private ThrottledAction SendGameStatePeriodicFunction;

    private GameObject SpawnPlayer(PlayerState playerState)
    {
        var spawnPoint = GetNextSpawnPoint(playerState);
        return SpawnPlayer(playerState.Id, spawnPoint.Position, spawnPoint.Orientation.eulerAngles.y);
    }
    private GameObject SpawnPlayer(uint playerId, Vector3 position, float lookDirYAngle)
    {
        var playerState = CurrentGameState.Players.First(ps => ps.Id == playerId);
        playerState.Position = position;
        playerState.LookDirAngles = new Vector2(0, lookDirYAngle);
        playerState.Health = OsFps.MaxPlayerHealth;
        playerState.Weapon0 = new WeaponState(WeaponType.Pistol, OsFps.PistolDefinition.MaxAmmo);
        playerState.Weapon1 = null;

        var playerObject = OsFps.Instance.SpawnLocalPlayer(playerState);

        /*var spawnPlayerMessage = new SpawnPlayerMessage
        {
            PlayerId = playerId,
            PlayerPosition = position,
            PlayerLookDirYAngle = lookDirYAngle
        };
        SendMessageToAllClients(reliableSequencedChannelId, spawnPlayerMessage);*/

        return playerObject;
    }

    private void OnMapLoaded(Scene scene, LoadSceneMode loadSceneMode)
    {
        SceneManager.sceneLoaded -= OnMapLoaded;

        CurrentGameState.WeaponObjects = GetWeaponObjectStatesFromGameObjects();
        SendGameStatePeriodicFunction = new ThrottledAction(SendGameState, 1.0f / 30);

        SpawnWeapons();

        if (OnServerStarted != null)
        {
            OnServerStarted();
        }
    }
    private RigidBodyState ToRigidBodyState(Rigidbody rigidbody)
    {
        return new RigidBodyState
        {
            Position = rigidbody.transform.position,
            EulerAngles = rigidbody.transform.eulerAngles,
            Velocity = rigidbody.velocity,
            AngularVelocity = rigidbody.angularVelocity
        };
    }
    private WeaponObjectState ToWeaponObjectState(WeaponComponent weaponComponent)
    {
        var state = new WeaponObjectState
        {
            Id = GenerateNetworkId(),
            Type = weaponComponent.Type,
            RigidBodyState = ToRigidBodyState(weaponComponent.Rigidbody)
        };

        weaponComponent.Id = state.Id;

        return state;
    }
    private List<WeaponObjectState> GetWeaponObjectStatesFromGameObjects()
    {
        var weaponObjectStates = new List<WeaponObjectState>();

        var weaponComponents = Object.FindObjectsOfType<WeaponComponent>();
        foreach (var weaponComponent in weaponComponents)
        {
            weaponObjectStates.Add(ToWeaponObjectState(weaponComponent));
        }

        return weaponObjectStates;
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

    private PositionOrientation3d GetNextSpawnPoint(PlayerState playerState)
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
        return new PositionOrientation3d
        {
            Position = new Vector3(0, 25, 0),
            Orientation = Quaternion.identity
        };
    }
    private void UpdatePlayers()
    {
        foreach (var playerState in CurrentGameState.Players)
        {
            if (!playerState.IsAlive)
            {
                playerState.RespawnTimeLeft -= Time.deltaTime;

                if (playerState.RespawnTimeLeft <= 0)
                {
                    SpawnPlayer(playerState);
                }
            }

            OsFps.Instance.UpdatePlayer(playerState);

            if (playerState.Position.y <= OsFps.KillPlaneY)
            {
                DamagePlayer(playerState, 9999, null);
            }
        }
    }
    private void PlayerPullTrigger(PlayerState playerState)
    {
        if (!playerState.CanShoot) return;

        var playerComponent = OsFps.Instance.FindPlayerComponent(playerState.Id);
        if (playerComponent == null) return;

        var shotRay = new Ray(
                playerComponent.CameraPointObject.transform.position,
                playerComponent.CameraPointObject.transform.forward
            );
        var raycastHits = Physics.RaycastAll(shotRay);

        foreach (var hit in raycastHits)
        {
            var hitPlayerObject = hit.collider.gameObject.FindObjectOrAncestorWithTag(OsFps.PlayerTag);

            if ((hitPlayerObject != null) && (hitPlayerObject != playerComponent.gameObject))
            {
                var hitPlayerComponent = hitPlayerObject.GetComponent<PlayerComponent>();
                var hitPlayerState = CurrentGameState.Players.Find(ps => ps.Id == hitPlayerComponent.Id);

                DamagePlayer(hitPlayerState, playerState.CurrentWeapon.Definition.DamagePerBullet, playerState);
            }
        }

        playerState.CurrentWeapon.BulletsLeftInMagazine--;
        playerState.CurrentWeapon.BulletsLeft--;
    }
    private void PlayerReload(PlayerState playerState)
    {
        var weapon = playerState.CurrentWeapon;
        if (weapon == null) return;

        var bulletsUsedInMagazine = weapon.Definition.BulletsPerMagazine - weapon.BulletsLeftInMagazine;
        var bulletsToAddToMagazine = (ushort)Mathf.Min(bulletsUsedInMagazine, weapon.BulletsLeftOutOfMagazine);
        weapon.BulletsLeftInMagazine += bulletsToAddToMagazine;
    }
    private void DamagePlayer(PlayerState playerState, int damage, PlayerState attackingPlayerState)
    {
        var playerComponent = OsFps.Instance.FindPlayerComponent(playerState.Id);
        if (playerComponent == null) return;

        playerState.Health -= damage;

        if (!playerState.IsAlive)
        {
            Object.Destroy(playerComponent.gameObject);

            playerState.Deaths++;

            if (attackingPlayerState != null)
            {
                attackingPlayerState.Kills++;
            }

            playerState.RespawnTimeLeft = OsFps.RespawnTime;
        }
    }

    private void SpawnWeapons()
    {
        var weaponSpawnerComponents = Object.FindObjectsOfType<WeaponSpawnerComponent>();

        foreach (var weaponSpawnerComponent in weaponSpawnerComponents)
        {
            var weaponObjectState = new WeaponObjectState
            {
                Id = GenerateNetworkId(),
                Type = weaponSpawnerComponent.WeaponType,
                RigidBodyState = new RigidBodyState
                {
                    Position = weaponSpawnerComponent.transform.position,
                    EulerAngles = weaponSpawnerComponent.transform.eulerAngles,
                    Velocity = Vector3.zero,
                    AngularVelocity = Vector3.zero
                }
            };
            var weaponObject = OsFps.Instance.SpawnLocalWeaponObject(weaponObjectState);

            var weaponComponent = weaponObject.GetComponent<WeaponComponent>();
            CurrentGameState.WeaponObjects.Add(weaponObjectState);
        }
    }

    private void UpdateRigidBodyStateFromRigidBody(RigidBodyState rigidBodyState, Rigidbody rigidbody)
    {
        rigidBodyState.Position = rigidbody.transform.position;
        rigidBodyState.EulerAngles = rigidbody.transform.eulerAngles;
        rigidBodyState.Velocity = rigidbody.velocity;
        rigidBodyState.AngularVelocity = rigidbody.angularVelocity;
    }
    private void UpdateGameStateFromObjects()
    {
        foreach(var playerState in CurrentGameState.Players)
        {
            var playerComponent = OsFps.Instance.FindPlayerComponent(playerState.Id);

            if (playerComponent != null)
            {
                playerState.Position = playerComponent.transform.position;
                playerState.Velocity = playerComponent.Rigidbody.velocity;
                playerState.LookDirAngles = OsFps.Instance.GetPlayerLookDirAngles(playerComponent);
            }
        }

        foreach(var weaponObjectState in CurrentGameState.WeaponObjects)
        {
            var weaponObject = OsFps.Instance.FindWeaponObject(weaponObjectState.Id);

            if (weaponObject != null)
            {
                var weaponComponent = weaponObject.GetComponent<WeaponComponent>();
                UpdateRigidBodyStateFromRigidBody(weaponObjectState.RigidBodyState, weaponComponent.Rigidbody);
            }
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
            default:
                throw new System.NotImplementedException("Unknown message type: " + messageType);
        }
    }
    private void HandlePlayerInputMessage(PlayerInputMessage message)
    {
        // TODO: Make sure the player ID is correct.
        var playerState = CurrentGameState.Players.First(ps => ps.Id == message.PlayerId);
        playerState.Input = message.PlayerInput;
        playerState.LookDirAngles = message.LookDirAngles;
    }
    private void HandleTriggerPulledMessage(TriggerPulledMessage message)
    {
        // TODO: Make sure the player ID is correct.
        var playerState = CurrentGameState.Players.First(ps => ps.Id == message.PlayerId);
        PlayerPullTrigger(playerState);
    }
    private void HandleReloadPressedMessage(ReloadPressedMessage message)
    {
        // TODO: Make sure the player ID is correct.
        var playerState = CurrentGameState.Players.First(ps => ps.Id == message.PlayerId);
        PlayerReload(playerState);
    }
    #endregion
}