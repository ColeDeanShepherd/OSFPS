using System.Linq;
using UnityEngine;
using Unity.Entities;
using System.Collections.Generic;
using UnityEngine.Assertions;

public class PlayerSystem : ComponentSystem
{
    public struct Group
    {
        public PlayerObjectComponent PlayerObjectComponent;
    }

    public static PlayerSystem Instance;

    public PlayerSystem()
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

        var client = OsFps.Instance?.Client;
        if (client != null)
        {
            ClientOnUpdate(client);
        }
    }

    private void ServerOnUpdate(Server server)
    {
        foreach (var entity in GetEntities<Group>())
        {
            var playerObjectState = entity.PlayerObjectComponent.State;
            var wasReloadingBeforeUpdate = playerObjectState.IsReloading;

            OsFps.Instance.UpdatePlayer(entity.PlayerObjectComponent);

            if (wasReloadingBeforeUpdate && (playerObjectState.ReloadTimeLeft <= 0))
            {
                ServerPlayerFinishReload(entity.PlayerObjectComponent);
            }
        }
    }

    public void ServerDamagePlayer(Server server, PlayerObjectComponent playerObjectComponent, float damage, PlayerObjectComponent attackingPlayerObjectComponent)
    {
        if (!playerObjectComponent.State.IsAlive || (damage <= 0)) return;

        var playerObjectState = playerObjectComponent.State;

        var damageToShield = Mathf.Min(damage, playerObjectState.Shield);
        playerObjectState.Shield -= damageToShield;

        var damageToHealth = damage - damageToShield;
        playerObjectState.Health -= damageToHealth;

        playerObjectState.TimeUntilShieldCanRegen = OsFps.TimeAfterDamageUntilShieldRegen;

        var playerComponent = OsFps.Instance.FindPlayerComponent(playerObjectState.Id);
        var playerState = playerComponent.State;

        if (!playerObjectState.IsAlive)
        {
            for (var i = 0; i < playerObjectComponent.State.Weapons.Length; i++)
            {
                ServerPlayerDropWeapon(server, playerObjectComponent, i);
            }

            for (var i = 0; i < playerObjectComponent.State.GrenadeSlots.Length; i++)
            {
                ServerPlayerDropGrenades(server, playerObjectComponent, i);
            }

            // Destroy the player.
            Object.Destroy(playerObjectComponent.gameObject);
            playerState.RespawnTimeLeft = OsFps.RespawnTime;

            // Update scores
            playerState.Deaths++;

            if (attackingPlayerObjectComponent != null)
            {
                var attackingPlayerId = attackingPlayerObjectComponent.State.Id;
                var attackingPlayerComponent = OsFps.Instance.FindPlayerComponent(attackingPlayerId);
                attackingPlayerComponent.State.Kills++;
            }

            // Send message.
            OsFps.Instance.CallRpcOnAllClients("ClientOnReceiveChatMessage", server.reliableSequencedChannelId, new
            {
                playerId = (uint?)null,
                message = GetKillMessage(playerObjectComponent, attackingPlayerObjectComponent)
            });
        }
    }

    public void ServerPlayerDropWeapon(Server server, PlayerObjectComponent playerObjectComponent, int weaponIndex)
    {
        var playerWeapons = playerObjectComponent.State.Weapons;
        var equippedWeaponState = playerWeapons[weaponIndex];
        if ((equippedWeaponState == null)) return;

        var weaponObjectState = new WeaponObjectState
        {
            Id = server.GenerateNetworkId(),
            Type = equippedWeaponState.Type,
            BulletsLeftInMagazine = equippedWeaponState.BulletsLeftInMagazine,
            BulletsLeftOutOfMagazine = equippedWeaponState.BulletsLeftOutOfMagazine,
            RigidBodyState = new RigidBodyState
            {
                Position = playerObjectComponent.HandsPointObject.transform.position,
                EulerAngles = Vector3.zero,
                Velocity = Vector3.zero,
                AngularVelocity = Vector3.zero
            }
        };
        OsFps.Instance.SpawnLocalWeaponObject(weaponObjectState);

        playerObjectComponent.State.Weapons[weaponIndex] = null;
    }
    public void ServerPlayerDropGrenades(Server server, PlayerObjectComponent playerObjectComponent, int grenadeSlotIndex)
    {
        var playerGrenadeSlots = playerObjectComponent.State.GrenadeSlots;
        var grenadeSlot = playerGrenadeSlots[grenadeSlotIndex];
        if ((grenadeSlot == null) || (grenadeSlot.GrenadeCount == 0)) return;

        for (var i = 0; i < grenadeSlot.GrenadeCount; i++)
        {
            var grenadeState = new GrenadeState
            {
                Id = server.GenerateNetworkId(),
                Type = grenadeSlot.GrenadeType,
                RigidBodyState = new RigidBodyState
                {
                    Position = playerObjectComponent.HandsPointObject.transform.position,
                    EulerAngles = Vector3.zero,
                    Velocity = Vector3.zero,
                    AngularVelocity = Vector3.zero
                },
                IsActive = false,
                TimeUntilDetonation = null
            };

            OsFps.Instance.SpawnLocalGrenadeObject(grenadeState);
        }

        playerGrenadeSlots[grenadeSlotIndex] = null;
    }

    public GameObject ServerSpawnPlayer(Server server, uint playerId)
    {
        var spawnPoint = server.GetNextSpawnPoint();
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
            ReloadTimeLeft = -1
        };
        var firstWeaponDefinition = OsFps.PistolDefinition;
        playerObjectState.Weapons[0] = new EquippedWeaponState
        {
            Type = firstWeaponDefinition.Type,
            BulletsLeftInMagazine = firstWeaponDefinition.BulletsPerMagazine,
            BulletsLeftOutOfMagazine = firstWeaponDefinition.MaxAmmoOutOfMagazine
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

        var playerObject = OsFps.Instance.SpawnLocalPlayer(playerObjectState);
        return playerObject;
    }

    public void ServerShoot(Server server, PlayerObjectComponent shootingPlayerObjectComponent)
    {
        var shootingPlayerObjectState = shootingPlayerObjectComponent.State;
        if (!shootingPlayerObjectState.CanShoot) return;

        var playerObjectComponent = OsFps.Instance.FindPlayerObjectComponent(shootingPlayerObjectState.Id);
        if (playerObjectComponent == null) return;

        var weaponState = shootingPlayerObjectState.CurrentWeapon;

        var shotRay = new Ray(
            playerObjectComponent.CameraPointObject.transform.position,
            playerObjectComponent.CameraPointObject.transform.forward
        );

        var weaponDefinition = weaponState.Definition;
        if (weaponState.Definition.IsHitScan)
        {
            ServerFireHitscanWeapon(server, shootingPlayerObjectComponent, weaponDefinition, shotRay);
        }
        else
        {
            if (weaponDefinition.Type == WeaponType.RocketLauncher)
            {
                ServerFireRocketLauncher(server, shootingPlayerObjectComponent, shotRay);
            }
        }

        weaponState.BulletsLeftInMagazine--;
        weaponState.TimeUntilCanShoot = weaponState.Definition.ShotInterval;
    }
    public void ServerFireHitscanWeapon(
        Server server, PlayerObjectComponent shootingPlayerObjectComponent,
        WeaponDefinition weaponDefinition, Ray shotRay
    )
    {
        var shootingPlayerObjectState = shootingPlayerObjectComponent.State;
        var playerObjectComponent = OsFps.Instance.FindPlayerObjectComponent(shootingPlayerObjectState.Id);

        var raycastHits = Physics.RaycastAll(shotRay);

        foreach (var hit in raycastHits)
        {
            var hitPlayerObject = hit.collider.gameObject.FindObjectOrAncestorWithTag(OsFps.PlayerTag);

            if ((hitPlayerObject != null) && (hitPlayerObject != playerObjectComponent.gameObject))
            {
                var hitPlayerObjectComponent = hitPlayerObject.GetComponent<PlayerObjectComponent>();

                var isHeadShot = hit.collider.gameObject.name == OsFps.PlayerHeadColliderName;
                var damage = !isHeadShot
                    ? weaponDefinition.DamagePerBullet
                    : weaponDefinition.HeadShotDamagePerBullet;
                ServerDamagePlayer(
                    server, hitPlayerObjectComponent, damage, shootingPlayerObjectComponent
                );
            }

            if (hit.rigidbody != null)
            {
                hit.rigidbody.AddForceAtPosition(5 * shotRay.direction, hit.point, ForceMode.Impulse);
            }
        }

        if (OsFps.ShowHitScanShotsOnServer)
        {
            CreateHitScanShotDebugLine(shotRay);
        }
    }
    public void CreateHitScanShotDebugLine(Ray ray)
    {
        var hitScanShotObject = new GameObject("Hit Scan Shot");

        var lineRenderer = hitScanShotObject.AddComponent<LineRenderer>();
        var lineWidth = 0.05f;
        lineRenderer.startWidth = lineWidth;
        lineRenderer.endWidth = lineWidth;
        lineRenderer.SetPositions(new Vector3[] {
            ray.origin,
            ray.origin + (1000 * ray.direction)
        });

        Object.Destroy(hitScanShotObject, OsFps.HitScanShotDebugLineLifetime);
    }
    public void ServerFireRocketLauncher(
        Server server, PlayerObjectComponent shootingPlayerObjectComponent, Ray shotRay
    )
    {
        var rocketState = new RocketState
        {
            Id = server.GenerateNetworkId(),
            RigidBodyState = new RigidBodyState
            {
                Position = shotRay.origin + shotRay.direction,
                EulerAngles = Quaternion.LookRotation(shotRay.direction, Vector3.up).eulerAngles,
                Velocity = OsFps.RocketSpeed * shotRay.direction,
                AngularVelocity = Vector3.zero
            },
            ShooterPlayerId = shootingPlayerObjectComponent.State.Id
        };
        var rocket = OsFps.Instance.SpawnLocalRocketObject(rocketState);
    }
    public void ServerPlayerPullTrigger(Server server, PlayerObjectComponent playerObjectComponent)
    {
        ServerShoot(server, playerObjectComponent);
    }
    public void ServerPlayerStartReload(PlayerObjectComponent playerObjectComponent)
    {
        var playerObjectState = playerObjectComponent.State;
        if (!playerObjectState.IsAlive) return;

        var weapon = playerObjectState.CurrentWeapon;
        if (weapon == null) return;

        playerObjectState.ReloadTimeLeft = weapon.Definition.ReloadTime;
        weapon.TimeUntilCanShoot = 0;
    }
    public void ServerPlayerFinishReload(PlayerObjectComponent playerObjectComponent)
    {
        var weapon = playerObjectComponent.State.CurrentWeapon;
        if (weapon == null) return;

        var bulletsUsedInMagazine = weapon.Definition.BulletsPerMagazine - weapon.BulletsLeftInMagazine;
        var bulletsToAddToMagazine = (ushort)Mathf.Min(bulletsUsedInMagazine, weapon.BulletsLeftOutOfMagazine);

        weapon.BulletsLeftInMagazine += bulletsToAddToMagazine;
        weapon.BulletsLeftOutOfMagazine -= bulletsToAddToMagazine;
    }

    public void ServerOnPlayerCollidingWithWeapon(Server server, GameObject playerObject, GameObject weaponObject)
    {
        /*var playerObjectComponent = playerObject.GetComponent<PlayerObjectComponent>();
        var weaponComponent = weaponObject.GetComponent<WeaponComponent>();

        ServerPlayerTryToPickupWeapon(playerObjectComponent, weaponComponent);*/
    }


    private EquippedWeaponState ToEquippedWeaponState(WeaponObjectState weaponObjectState)
    {
        return new EquippedWeaponState
        {
            Type = weaponObjectState.Type,
            BulletsLeftInMagazine = weaponObjectState.BulletsLeftInMagazine,
            BulletsLeftOutOfMagazine = weaponObjectState.BulletsLeftOutOfMagazine,
            TimeUntilCanShoot = 0
        };
    }
    public void ServerPlayerTryToPickupWeapon(
        Server server, PlayerObjectComponent playerObjectComponent, WeaponComponent weaponComponent
    )
    {
        var playerState = playerObjectComponent.State;
        if (!playerState.IsAlive) return;

        var weaponObjectState = weaponComponent.State;
        var playersMatchingWeapon = playerState.Weapons.FirstOrDefault(
            w => (w != null) && (w.Type == weaponComponent.State.Type)
        );

        if (playerState.HasEmptyWeapon)
        {
            var emptyWeaponIndex = System.Array.FindIndex(playerState.Weapons, w => w == null);
            playerState.Weapons[emptyWeaponIndex] = ToEquippedWeaponState(weaponObjectState);
            playerState.CurrentWeaponIndex = (byte)emptyWeaponIndex;

            Object.Destroy(weaponComponent.gameObject);
        }
        else if (playersMatchingWeapon != null)
        {
            var numBulletsPickedUp = WeaponSystem.Instance.ServerAddBullets(
                playersMatchingWeapon, weaponObjectState.BulletsLeft
            );
            WeaponSystem.Instance.ServerRemoveBullets(weaponObjectState, numBulletsPickedUp);

            if (weaponObjectState.BulletsLeft == 0)
            {
                Object.Destroy(weaponComponent.gameObject);
            }
        }
        else
        {
            // drop current weapon
            ServerPlayerDropWeapon(server, playerObjectComponent, playerState.CurrentWeaponIndex);

            // pick up other weapon
            playerState.Weapons[playerState.CurrentWeaponIndex] = ToEquippedWeaponState(weaponObjectState);
            Object.Destroy(weaponComponent.gameObject);
        }
    }
    public void ServerOnPlayerCollidingWithGrenade(GameObject playerObject, GameObject grenadeObject)
    {
        var playerObjectComponent = playerObject.GetComponent<PlayerObjectComponent>();
        var playerState = playerObjectComponent.State;

        if (!playerState.IsAlive) return;

        var grenadeComponent = grenadeObject.GetComponent<GrenadeComponent>();
        var grenadeState = grenadeComponent.State;

        if (grenadeState.IsActive) return;

        // Try to find a grenade slot with a matching grenade type.
        var grenadeSlot = playerState.GrenadeSlots.FirstOrDefault(gs =>
            (gs != null) &&
            (gs.GrenadeType == grenadeState.Type)
        );

        // Try to find a grenade slot with a different type but 0 grenades.
        if (grenadeSlot == null)
        {
            grenadeSlot = playerState.GrenadeSlots.FirstOrDefault(gs =>
                (gs != null) &&
                (gs.GrenadeCount == 0)
            );
            grenadeSlot.GrenadeType = grenadeState.Type;
        }

        // Try to find a null grenade slot.
        if (grenadeSlot == null)
        {
            var nullSlotIndex = System.Array.FindIndex(playerState.GrenadeSlots, gs => gs == null);
            if (nullSlotIndex >= 0)
            {
                grenadeSlot = new GrenadeSlot
                {
                    GrenadeType = grenadeState.Type,
                    GrenadeCount = 0
                };
                playerState.GrenadeSlots[nullSlotIndex] = grenadeSlot;
            }
        }

        if ((grenadeSlot == null) || (grenadeSlot.GrenadeCount >= OsFps.MaxGrenadesPerType)) return;

        grenadeSlot.GrenadeCount++;
        Object.Destroy(grenadeObject);
    }

    public void UpdatePlayerMovement(PlayerObjectState playerObjectState)
    {
        var playerObjectComponent = OsFps.Instance.FindPlayerObjectComponent(playerObjectState.Id);
        if (playerObjectComponent == null) return;

        OsFps.Instance.ApplyLookDirAnglesToPlayer(playerObjectComponent, playerObjectState.LookDirAngles);

        var isGrounded = OsFps.Instance.IsPlayerGrounded(playerObjectComponent);

        if (isGrounded)
        {
            var relativeMoveDirection = OsFps.Instance.GetRelativeMoveDirection(playerObjectState.Input);
            var playerYAngle = playerObjectComponent.transform.eulerAngles.y;
            var horizontalMoveDirection = Quaternion.Euler(new Vector3(0, playerYAngle, 0)) * relativeMoveDirection;
            var desiredHorizontalVelocity = OsFps.MaxPlayerMovementSpeed * horizontalMoveDirection;
            var currentHorizontalVelocity = GameObjectExtensions.GetHorizontalVelocity(playerObjectComponent.Rigidbody);
            var horizontalVelocityError = desiredHorizontalVelocity - currentHorizontalVelocity;

            playerObjectComponent.Rigidbody.AddForce(3000 * horizontalVelocityError);
        }
    }
    public void Jump(PlayerObjectComponent playerObjectComponent)
    {
        Assert.IsNotNull(playerObjectComponent);

        var playerVelocity = playerObjectComponent.Rigidbody.velocity;
        var newPlayerVelocity = new Vector3(playerVelocity.x, OsFps.PlayerInitialJumpSpeed, playerVelocity.z);
        playerObjectComponent.Rigidbody.velocity = newPlayerVelocity;
    }

    private string GetKillMessage(PlayerObjectComponent killedPlayerObjectComponent, PlayerObjectComponent attackerPlayerObjectComponent)
    {
        return (attackerPlayerObjectComponent != null)
            ? string.Format("{0} killed {1}.", attackerPlayerObjectComponent.State.Id, killedPlayerObjectComponent.State.Id)
            : string.Format("{0} died.", killedPlayerObjectComponent.State.Id);
    }

    private void ClientOnUpdate(Client client)
    {
        foreach (var entity in GetEntities<Group>())
        {
            var playerObjectComponent = entity.PlayerObjectComponent;
            var playerObjectState = playerObjectComponent.State;

            if (playerObjectState.Id == client.PlayerId)
            {
                if (!client._isShowingChatMessageInput && !client._isShowingMenu)
                {
                    ClientUpdateThisPlayer(client, playerObjectComponent);
                }
                else
                {
                    playerObjectState.Input = new PlayerInput();
                }
            }

            OsFps.Instance.UpdatePlayer(playerObjectComponent);
        }
    }
    private void ClientUpdateThisPlayer(Client client, PlayerObjectComponent playerObjectComponent)
    {
        var playerObjectState = playerObjectComponent.State;
        playerObjectState.Input = OsFps.Instance.GetCurrentPlayersInput();

        var mouseSensitivity = 3;
        var deltaMouse = mouseSensitivity * new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));

        playerObjectState.LookDirAngles = new Vector2(
            Mathf.Clamp(MathfExtensions.ToSignedAngleDegrees(playerObjectState.LookDirAngles.x - deltaMouse.y), -90, 90),
            Mathf.Repeat(playerObjectState.LookDirAngles.y + deltaMouse.x, 360)
        );

        if (Input.GetKeyDown(OsFps.ReloadKeyCode) && playerObjectState.CanReload)
        {
            client.Reload(playerObjectState);
        }

        if (playerObjectState.Input.IsFirePressed)
        {
            var wasTriggerJustPulled = Input.GetMouseButtonDown(OsFps.FireMouseButtonNumber);

            if (
                playerObjectState.CanShoot &&
                (wasTriggerJustPulled || playerObjectState.CurrentWeapon.Definition.IsAutomatic)
            )
            {
                client.Shoot(playerObjectComponent);
            }
        }

        if (Input.GetMouseButtonDown(OsFps.ThrowGrenadeMouseButtonNumber) && playerObjectState.CanThrowGrenade)
        {
            client.ThrowGrenade(playerObjectState);
        }

        if (Input.GetKeyDown(OsFps.SwitchGrenadeTypeKeyCode))
        {
            client.SwitchGrenadeType(playerObjectState);
        }
    }
}