using System.Linq;
using UnityEngine;
using Unity.Entities;

public class PlayerSystem : ComponentSystem
{
    public struct Group
    {
        public PlayerComponent PlayerComponent;
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
            var playerState = server.CurrentGameState.Players
                .FirstOrDefault(g => g.Id == entity.PlayerComponent.Id);

            var wasReloadingBeforeUpdate = playerState.IsReloading;

            OsFps.Instance.UpdatePlayer(playerState);

            if (wasReloadingBeforeUpdate && (playerState.ReloadTimeLeft <= 0))
            {
                ServerPlayerFinishReload(playerState);
            }
        }
    }

    public void ServerDamagePlayer(Server server, PlayerState playerState, int damage, PlayerState attackingPlayerState)
    {
        var playerComponent = OsFps.Instance.FindPlayerComponent(playerState.Id);
        if (playerComponent == null) return;

        playerState.Health -= damage;

        if (!playerState.IsAlive)
        {
            // Destroy the player.
            Object.Destroy(playerComponent.gameObject);
            playerState.RespawnTimeLeft = OsFps.RespawnTime;

            // Update scores
            playerState.Deaths++;

            if (attackingPlayerState != null)
            {
                attackingPlayerState.Kills++;
            }

            // Send message.
            var chatMessage = new ChatMessage
            {
                PlayerId = null,
                Message = GetKillMessage(playerState, attackingPlayerState)
            };

            server.SendMessageToAllClients(server.reliableSequencedChannelId, chatMessage);
        }
    }

    public GameObject ServerSpawnPlayer(Server server, PlayerState playerState)
    {
        var spawnPoint = server.GetNextSpawnPoint(playerState);
        return ServerSpawnPlayer(server, playerState.Id, spawnPoint.Position, spawnPoint.Orientation.eulerAngles.y);
    }
    public GameObject ServerSpawnPlayer(Server server, uint playerId, Vector3 position, float lookDirYAngle)
    {
        var playerState = server.CurrentGameState.Players.First(ps => ps.Id == playerId);
        playerState.Position = position;
        playerState.LookDirAngles = new Vector2(0, lookDirYAngle);
        playerState.Health = OsFps.MaxPlayerHealth;
        playerState.CurrentWeaponIndex = 0;
        playerState.Weapons[0] = new WeaponState(WeaponType.Pistol, OsFps.PistolDefinition.MaxAmmo);
        playerState.GrenadesLeftByType[GrenadeType.Fragmentation] = 2;
        playerState.GrenadesLeftByType[GrenadeType.Sticky] = 2;

        for (var i = 1; i < playerState.Weapons.Length; i++)
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

    public void ServerShoot(Server server, PlayerState shootingPlayerState)
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
                var hitPlayerState = server.CurrentGameState.Players.Find(ps => ps.Id == hitPlayerComponent.Id);

                ServerDamagePlayer(server, hitPlayerState, weaponState.Definition.DamagePerBullet, shootingPlayerState);
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
    public void ServerPlayerPullTrigger(Server server, PlayerState playerState)
    {
        ServerShoot(server, playerState);
    }
    public void ServerPlayerStartReload(PlayerState playerState)
    {
        if (!playerState.IsAlive) return;

        var weapon = playerState.CurrentWeapon;
        if (weapon == null) return;

        playerState.ReloadTimeLeft = weapon.Definition.ReloadTime;
        weapon.TimeUntilCanShoot = 0;
    }
    public void ServerPlayerFinishReload(PlayerState playerState)
    {
        var weapon = playerState.CurrentWeapon;
        if (weapon == null) return;

        var bulletsUsedInMagazine = weapon.Definition.BulletsPerMagazine - weapon.BulletsLeftInMagazine;
        var bulletsToAddToMagazine = (ushort)Mathf.Min(bulletsUsedInMagazine, weapon.BulletsLeftOutOfMagazine);
        weapon.BulletsLeftInMagazine += bulletsToAddToMagazine;
    }

    public void ServerOnPlayerCollidingWithWeapon(Server server, GameObject playerObject, GameObject weaponObject)
    {
        var playerComponent = playerObject.GetComponent<PlayerComponent>();
        var playerState = server.CurrentGameState.Players.First(ps => ps.Id == playerComponent.Id);
        var weaponComponent = weaponObject.GetComponent<WeaponComponent>();
        var weaponObjectState = server.CurrentGameState.WeaponObjects.First(wos => wos.Id == weaponComponent.Id);

        var playersMatchingWeapon = playerState.Weapons.FirstOrDefault(
            w => (w != null) && (w.Type == weaponComponent.Type)
        );

        if (playersMatchingWeapon != null)
        {
            var numBulletsPickedUp = WeaponSystem.Instance.ServerAddBullets(playersMatchingWeapon, weaponObjectState.BulletsLeft);
            WeaponSystem.Instance.ServerRemoveBullets(weaponObjectState, numBulletsPickedUp);

            if (weaponObjectState.BulletsLeft == 0)
            {
                var weaponObjectId = weaponComponent.Id;
                Object.Destroy(weaponObject);
                server.CurrentGameState.WeaponObjects.RemoveAll(wos => wos.Id == weaponObjectId);
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
            server.CurrentGameState.WeaponObjects.RemoveAll(wos => wos.Id == weaponObjectId);
        }
    }
    public void ServerOnPlayerCollidingWithGrenade(GameObject playerObject, GameObject weaponObject)
    {
        Debug.Log("TODO: Implement OnPlayerCollidingWithGrenade");
        // ONLY PICK UP IF NOT ACTIVE
    }

    private string GetKillMessage(PlayerState killedPlayerState, PlayerState attackerPlayerState)
    {
        return (attackerPlayerState != null)
            ? string.Format("{0} killed {1}.", attackerPlayerState.Id, killedPlayerState.Id)
            : string.Format("{0} died.", killedPlayerState.Id);
    }

    private void ClientOnUpdate(Client client)
    {
        foreach (var playerState in client.CurrentGameState.Players)
        {
            if (playerState.Id == client.PlayerId)
            {
                if (!client._isShowingChatMessageInput && !client._isShowingMenu)
                {
                    ClientUpdateThisPlayer(client, playerState);
                }
                else
                {
                    playerState.Input = new PlayerInput();
                }
            }

            OsFps.Instance.UpdatePlayer(playerState);
        }
    }
    private void ClientUpdateThisPlayer(Client client, PlayerState playerState)
    {
        playerState.Input = OsFps.Instance.GetCurrentPlayersInput();

        var mouseSensitivity = 3;
        var deltaMouse = mouseSensitivity * new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));

        playerState.LookDirAngles = new Vector2(
            Mathf.Clamp(MathfExtensions.ToSignedAngleDegrees(playerState.LookDirAngles.x - deltaMouse.y), -90, 90),
            Mathf.Repeat(playerState.LookDirAngles.y + deltaMouse.x, 360)
        );

        if (Input.GetKeyDown(OsFps.ReloadKeyCode))
        {
            client.Reload(playerState);
        }

        if (playerState.Input.IsFirePressed)
        {
            var wasTriggerJustPulled = Input.GetMouseButtonDown(OsFps.FireMouseButtonNumber);

            if (
                playerState.CanShoot &&
                (wasTriggerJustPulled || playerState.CurrentWeapon.Definition.IsAutomatic)
            )
            {
                client.Shoot(playerState);
            }
        }

        if (Input.GetKeyDown(OsFps.ThrowGrenadeKeyCode) && playerState.CanThrowGrenade)
        {
            client.ThrowGrenade(playerState);
        }
    }
}