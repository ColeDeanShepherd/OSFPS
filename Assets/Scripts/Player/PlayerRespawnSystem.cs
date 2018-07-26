using System.Collections.Generic;
using System.Linq;
using Unity.Entities;
using UnityEngine;

public class PlayerRespawnSystem : ComponentSystem
{
    public struct Data
    {
        public PlayerComponent PlayerComponent;
    }

    public static PlayerRespawnSystem Instance;

    public PlayerRespawnSystem()
    {
        Instance = this;
    }
    protected override void OnUpdate()
    {
        var server = OsFps.Instance?.Server;
        if (server != null)
        {
            ServerOnUpdate(server);
        }
    }

    private void ServerOnUpdate(Server server)
    {
        var playersToSpawnStates = new List<PlayerState>();

        foreach (var entity in GetEntities<Data>())
        {
            var playerState = entity.PlayerComponent.State;
            if (playerState.RespawnTimeLeft > 0)
            {
                playerState.RespawnTimeLeft -= Time.deltaTime;

                if (playerState.RespawnTimeLeft <= 0)
                {
                    playersToSpawnStates.Add(playerState);
                }
            }
        }

        foreach (var playersToSpawnState in playersToSpawnStates)
        {
            ServerSpawnPlayer(server, playersToSpawnState.Id);
        }
    }

    public GameObject ServerSpawnPlayer(Server server, uint playerId)
    {
        var spawnPoint = GetNextSpawnPoint();
        return ServerSpawnPlayer(server, playerId, spawnPoint.Position, spawnPoint.Orientation.eulerAngles.y);
    }
    public GameObject ServerSpawnPlayer(Server server, uint playerId, Vector3 position, float lookDirYAngle)
    {
        var playerObjectState = new PlayerObjectState
        {
            Id = playerId,
            Position = position,
            Velocity = Vector3.zero,
            LookDirAngles = new Vector2(0, lookDirYAngle),
            Input = new PlayerInput(),
            Health = OsFps.MaxPlayerHealth,
            Shield = OsFps.MaxPlayerShield,
            Weapons = new EquippedWeaponState[OsFps.MaxWeaponCount],
            CurrentWeaponIndex = 0,
            TimeUntilCanThrowGrenade = 0,
            CurrentGrenadeSlotIndex = 0,
            GrenadeSlots = new GrenadeSlot[OsFps.MaxGrenadeSlotCount],
            ReloadTimeLeft = -1,
            EquipWeaponTimeLeft = -1
        };
        var firstWeaponDefinition = WeaponSystem.Instance.GetWeaponDefinitionByType(WeaponType.Pistol);
        playerObjectState.Weapons[0] = new EquippedWeaponState
        {
            Type = firstWeaponDefinition.Type,
            BulletsLeftInMagazine = firstWeaponDefinition.BulletsPerMagazine,
            BulletsLeftOutOfMagazine = firstWeaponDefinition.MaxAmmoOutOfMagazine,
            TimeSinceLastShot = firstWeaponDefinition.ShotInterval
        };

        playerObjectState.GrenadeSlots[0] = new GrenadeSlot
        {
            GrenadeType = GrenadeType.Fragmentation,
            GrenadeCount = 2
        };
        playerObjectState.GrenadeSlots[1] = new GrenadeSlot
        {
            GrenadeType = GrenadeType.Sticky,
            GrenadeCount = 2
        };

        var playerObject = SpawnLocalPlayer(playerObjectState);
        return playerObject;
    }

    public GameObject ClientSpawnPlayer(Client client, PlayerObjectState playerState)
    {
        var playerObject = SpawnLocalPlayer(playerState);

        if (playerState.Id == client.PlayerId)
        {
            client.AttachCameraToPlayer(playerState.Id);
            client.HidePlayerModelFromCamera(playerObject);
        }

        return playerObject;
    }

    public GameObject SpawnLocalPlayer(PlayerObjectState playerObjectState)
    {
        var orientation = Quaternion.Euler(
            playerObjectState.LookDirAngles.x,
            playerObjectState.LookDirAngles.y,
            0
        );
        var playerObject = GameObject.Instantiate(
            OsFps.Instance.PlayerPrefab,
            playerObjectState.Position,
            orientation
        );

        var playerObjectComponent = playerObject.GetComponent<PlayerObjectComponent>();
        playerObjectComponent.State = playerObjectState;
        playerObjectComponent.Rigidbody.velocity = playerObjectState.Velocity;

        PlayerObjectSystem.Instance.SetShieldAlpha(playerObjectComponent, 0);

        return playerObject;
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
}