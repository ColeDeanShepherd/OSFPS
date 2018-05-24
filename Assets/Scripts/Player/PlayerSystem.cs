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
        var server = OsFps.Instance.Server;
        if (server != null)
        {
            ServerOnUpdate(server);
        }

        var client = OsFps.Instance.Client;
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

    public void ServerDamagePlayer(Server server, PlayerObjectComponent playerObjectComponent, int damage, PlayerObjectComponent attackingPlayerObjectComponent)
    {
        var playerObjectState = playerObjectComponent.State;
        playerObjectState.Health -= damage;

        var playerComponent = OsFps.Instance.FindPlayerComponent(playerObjectState.Id);
        var playerState = playerComponent.State;

        if (!playerObjectState.IsAlive)
        {
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
            Weapons = new WeaponObjectState[OsFps.MaxWeaponCount],
            CurrentWeaponIndex = 0,
            TimeUntilCanThrowGrenade = 0,
            CurrentGrenadeType = GrenadeType.Fragmentation,
            GrenadesLeftByType = new Dictionary<GrenadeType, byte>(),
            ReloadTimeLeft = -1
        };
        var firstWeaponDefinition = OsFps.PistolDefinition;
        playerObjectState.Weapons[0] = new WeaponObjectState
        {
            Type = firstWeaponDefinition.Type,
            BulletsLeftInMagazine = firstWeaponDefinition.BulletsPerMagazine,
            BulletsLeftOutOfMagazine = firstWeaponDefinition.MaxAmmoOutOfMagazine
        };
        playerObjectState.GrenadesLeftByType[GrenadeType.Fragmentation] = 2;
        playerObjectState.GrenadesLeftByType[GrenadeType.Sticky] = 2;

        for (var i = 1; i < playerObjectState.Weapons.Length; i++)
        {
            playerObjectState.Weapons[i] = null;
        }

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
        var raycastHits = Physics.RaycastAll(shotRay);

        foreach (var hit in raycastHits)
        {
            var hitPlayerObject = hit.collider.gameObject.FindObjectOrAncestorWithTag(OsFps.PlayerTag);

            if ((hitPlayerObject != null) && (hitPlayerObject != playerObjectComponent.gameObject))
            {
                var hitPlayerObjectComponent = hitPlayerObject.GetComponent<PlayerObjectComponent>();

                var isHeadShot = hit.collider.gameObject.name == OsFps.PlayerHeadColliderName;
                var damage = !isHeadShot
                    ? weaponState.Definition.DamagePerBullet
                    : weaponState.Definition.HeadShotDamagePerBullet;
                ServerDamagePlayer(
                    server, hitPlayerObjectComponent, damage, shootingPlayerObjectComponent
                );
            }

            if (hit.rigidbody != null)
            {
                hit.rigidbody.AddForceAtPosition(5 * shotRay.direction, hit.point, ForceMode.Impulse);
            }
        }

        weaponState.BulletsLeftInMagazine--;

        weaponState.TimeUntilCanShoot = weaponState.Definition.ShotInterval;
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
        var playerObjectComponent = playerObject.GetComponent<PlayerObjectComponent>();
        var playerState = playerObjectComponent.State;
        var weaponComponent = weaponObject.GetComponent<WeaponComponent>();
        var weaponObjectState = weaponComponent.State;

        var playersMatchingWeapon = playerState.Weapons.FirstOrDefault(
            w => (w != null) && (w.Type == weaponComponent.Type)
        );

        if (playersMatchingWeapon != null)
        {
            var numBulletsPickedUp = WeaponSystem.Instance.ServerAddBullets(
                playersMatchingWeapon, weaponObjectState.BulletsLeft
            );
            WeaponSystem.Instance.ServerRemoveBullets(weaponObjectState, numBulletsPickedUp);

            if (weaponObjectState.BulletsLeft == 0)
            {
                var weaponObjectId = weaponComponent.State.Id;
                Object.Destroy(weaponObject);
            }
        }
        else if (playerState.HasEmptyWeapon)
        {
            var emptyWeaponIndex = System.Array.FindIndex(playerState.Weapons, w => w == null);
            playerState.Weapons[emptyWeaponIndex] = weaponObjectState;

            var weaponObjectId = weaponComponent.State.Id;
            Object.Destroy(weaponObject);
        }
    }
    public void ServerOnPlayerCollidingWithGrenade(GameObject playerObject, GameObject weaponObject)
    {
        Debug.Log("TODO: Implement OnPlayerCollidingWithGrenade");
        // ONLY PICK UP IF NOT ACTIVE
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

        if (Input.GetKeyDown(OsFps.ReloadKeyCode))
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

        if (Input.GetKeyDown(OsFps.ThrowGrenadeKeyCode) && playerObjectState.CanThrowGrenade)
        {
            client.ThrowGrenade(playerObjectState);
        }
    }
}