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

        UpdateWeaponSpawners();
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

    public int AddBullets(WeaponState weaponState, int numBulletsToTryToAdd)
    {
        var numBulletsCanAdd = weaponState.Definition.MaxAmmo - weaponState.BulletsLeft;
        var bulletsToPickUp = Mathf.Min(numBulletsToTryToAdd, numBulletsCanAdd);

        weaponState.BulletsLeft += (ushort)bulletsToPickUp;
        weaponState.BulletsLeftInMagazine = (ushort)Mathf.Min(
            weaponState.BulletsLeftInMagazine + bulletsToPickUp,
            weaponState.Definition.BulletsPerMagazine
        );

        return bulletsToPickUp;
    }
    public void RemoveBullets(WeaponObjectState weaponObjectState, int numBulletsToRemove)
    {
        var bulletsToRemoveFromMagazine = Mathf.Min(weaponObjectState.BulletsLeftInMagazine, numBulletsToRemove);
        weaponObjectState.BulletsLeftInMagazine -= (ushort)bulletsToRemoveFromMagazine;
        numBulletsToRemove -= bulletsToRemoveFromMagazine;

        if (numBulletsToRemove > 0)
        {
            weaponObjectState.BulletsLeftOutOfMagazine -= (ushort)Mathf.Min(
                weaponObjectState.BulletsLeftOutOfMagazine,
                numBulletsToRemove
            );
        }
    }
    public void OnPlayerCollidingWithWeapon(GameObject playerObject, GameObject weaponObject)
    {
        var playerComponent = playerObject.GetComponent<PlayerComponent>();
        var playerState = CurrentGameState.Players.First(ps => ps.Id == playerComponent.Id);
        var weaponComponent = weaponObject.GetComponent<WeaponComponent>();
        var weaponObjectState = CurrentGameState.WeaponObjects.First(wos => wos.Id == weaponComponent.Id);

        var playersMatchingWeapon = playerState.Weapons.FirstOrDefault(
            w => (w != null) && (w.Type == weaponComponent.Type)
        );

        if (playersMatchingWeapon != null)
        {
            var numBulletsPickedUp = AddBullets(playersMatchingWeapon, weaponObjectState.BulletsLeft);
            RemoveBullets(weaponObjectState, numBulletsPickedUp);

            if(weaponObjectState.BulletsLeft == 0)
            {
                var weaponObjectId = weaponComponent.Id;
                Object.Destroy(weaponObject);
                CurrentGameState.WeaponObjects.RemoveAll(wos => wos.Id == weaponObjectId);
            }
        }
        else if (playerState.HasEmptyWeapon)
        {
            var emptyWeaponIndex = System.Array.FindIndex(playerState.Weapons, w => w == null);
            playerState.Weapons[emptyWeaponIndex] = new WeaponState
            {
                Type = weaponObjectState.Type,
                BulletsLeft = weaponObjectState.BulletsLeft,
                BulletsLeftInMagazine = weaponObjectState.BulletsLeftInMagazine
            };

            var weaponObjectId = weaponComponent.Id;
            Object.Destroy(weaponObject);
            CurrentGameState.WeaponObjects.RemoveAll(wos => wos.Id == weaponObjectId);
        }
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
        playerState.CurrentWeaponIndex = 0;
        playerState.Weapons[0] = new WeaponState(WeaponType.Pistol, OsFps.PistolDefinition.MaxAmmo);

        for(var i = 1; i < playerState.Weapons.Length; i++)
        {
            playerState.Weapons[i] = null;
        }

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
        CurrentGameState.WeaponSpawners = GetWeaponSpawnerStatesFromGameObjects();
        SendGameStatePeriodicFunction = new ThrottledAction(SendGameState, 1.0f / 30);

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
            BulletsLeftInMagazine = weaponComponent.BulletsLeftInMagazine,
            BulletsLeftOutOfMagazine = weaponComponent.BulletsLeftOutOfMagazine,
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

    private WeaponSpawnerState ToWeaponSpawnerState(WeaponSpawnerComponent weaponSpawnerComponent)
    {
        var state = new WeaponSpawnerState
        {
            Id = GenerateNetworkId(),
            Type = weaponSpawnerComponent.WeaponType,
            TimeUntilNextSpawn = 0
        };

        return state;
    }
    private List<WeaponSpawnerState> GetWeaponSpawnerStatesFromGameObjects()
    {
        var weaponSpawnerStates = new List<WeaponSpawnerState>();

        var weaponSpawnerComponents = Object.FindObjectsOfType<WeaponSpawnerComponent>();
        foreach (var weaponSpawnerComponent in weaponSpawnerComponents)
        {
            var weaponSpawnerState = ToWeaponSpawnerState(weaponSpawnerComponent);
            weaponSpawnerStates.Add(weaponSpawnerState);

            weaponSpawnerComponent.Id = weaponSpawnerState.Id;
        }

        return weaponSpawnerStates;
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

    private void UpdateWeaponSpawners()
    {
        foreach (var weaponSpawner in CurrentGameState.WeaponSpawners)
        {
            // shot interval
            if (weaponSpawner.TimeUntilNextSpawn > 0)
            {
                weaponSpawner.TimeUntilNextSpawn -= Time.deltaTime;
            }

            if (weaponSpawner.TimeUntilNextSpawn <= 0)
            {
                SpawnWeapon(weaponSpawner);
                weaponSpawner.TimeUntilNextSpawn += OsFps.GetWeaponDefinitionByType(weaponSpawner.Type).SpawnInterval;
            }
        }
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
            // respawn
            if (!playerState.IsAlive)
            {
                playerState.RespawnTimeLeft -= Time.deltaTime;

                if (playerState.RespawnTimeLeft <= 0)
                {
                    SpawnPlayer(playerState);
                }
            }

            // reload
            if (playerState.IsReloading)
            {
                playerState.ReloadTimeLeft -= Time.deltaTime;

                if (playerState.ReloadTimeLeft <= 0)
                {
                    PlayerFinishReload(playerState);
                }
            }

            // shot interval
            if ((playerState.CurrentWeapon != null) && (playerState.CurrentWeapon.TimeUntilCanShoot > 0))
            {
                playerState.CurrentWeapon.TimeUntilCanShoot -= Time.deltaTime;
            }

            // update movement
            OsFps.Instance.UpdatePlayerMovement(playerState);

            // kill if too low in map
            if (playerState.Position.y <= OsFps.KillPlaneY)
            {
                DamagePlayer(playerState, 9999, null);
            }
        }
    }
    private void Shoot(PlayerState shootingPlayerState)
    {
        if (!shootingPlayerState.CanShoot) return;

        var playerComponent = OsFps.Instance.FindPlayerComponent(shootingPlayerState.Id);
        if (playerComponent == null) return;

        var weaponState = shootingPlayerState.CurrentWeapon;

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

                DamagePlayer(hitPlayerState, weaponState.Definition.DamagePerBullet, shootingPlayerState);
            }

            if (hit.rigidbody != null)
            {
                hit.rigidbody.AddForceAtPosition(5 * shotRay.direction, hit.point, ForceMode.Impulse);
            }
        }

        weaponState.BulletsLeftInMagazine--;
        weaponState.BulletsLeft--;

        weaponState.TimeUntilCanShoot = weaponState.Definition.ShotInterval;
    }
    private void PlayerPullTrigger(PlayerState playerState)
    {
        Shoot(playerState);
    }
    private void PlayerStartReload(PlayerState playerState)
    {
        if (!playerState.IsAlive) return;

        var weapon = playerState.CurrentWeapon;
        if (weapon == null) return;

        playerState.ReloadTimeLeft = weapon.Definition.ReloadTime;
        weapon.TimeUntilCanShoot = 0;
    }
    private void PlayerFinishReload(PlayerState playerState)
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

    private void SpawnWeapon(WeaponSpawnerState weaponSpawnerState)
    {
        if (weaponSpawnerState.TimeUntilNextSpawn > 0) return;

        var weaponDefinition = OsFps.GetWeaponDefinitionByType(weaponSpawnerState.Type);
        var bulletsLeft = weaponDefinition.MaxAmmo / 2;
        var bulletsLeftInMagazine = Mathf.Min(weaponDefinition.BulletsPerMagazine, bulletsLeft);
        var weaponSpawnerComponent = OsFps.Instance.FindWeaponSpawnerComponent(weaponSpawnerState.Id);

        var weaponObjectState = new WeaponObjectState
        {
            Id = GenerateNetworkId(),
            Type = weaponSpawnerState.Type,
            BulletsLeftInMagazine = (ushort)bulletsLeftInMagazine,
            BulletsLeftOutOfMagazine = (ushort)(bulletsLeft - bulletsLeftInMagazine),
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
            else
            {
                // remove weapon object state???
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
        var playerState = CurrentGameState.Players.First(ps => ps.Id == message.PlayerId);
        playerState.Input = message.PlayerInput;
        playerState.LookDirAngles = message.LookDirAngles;
    }
    private void HandleTriggerPulledMessage(TriggerPulledMessage message)
    {
        // TODO: Make sure the player ID is correct.
        var playerState = CurrentGameState.Players.First(ps => ps.Id == message.PlayerId);
        PlayerPullTrigger(playerState);

        SendMessageToAllClients(reliableSequencedChannelId, message);
    }
    private void HandleReloadPressedMessage(ReloadPressedMessage message)
    {
        // TODO: Make sure the player ID is correct.
        var playerState = CurrentGameState.Players.First(ps => ps.Id == message.PlayerId);

        if (playerState.CanReload)
        {
            PlayerStartReload(playerState);
        }

        // TODO: Send to all other players???
    }
    private void HandleChatMessage(ChatMessage message)
    {
        SendMessageToAllClients(reliableSequencedChannelId, message);
    }
    private void HandleChangeWeaponMessage(ChangeWeaponMessage message)
    {
        SendMessageToAllClients(reliableSequencedChannelId, message);

        var playerState = CurrentGameState.Players.FirstOrDefault(ps => ps.Id == message.PlayerId);
        if (playerState == null) return;

        playerState.CurrentWeaponIndex = message.WeaponIndex;
        playerState.ReloadTimeLeft = -1;
    }
    #endregion
}