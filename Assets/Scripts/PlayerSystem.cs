using System.Linq;
using UnityEngine;

public class PlayerSystem
{
    public void OnUpdate()
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
        foreach (var playerState in server.CurrentGameState.Players)
        {
            // respawn
            if (!playerState.IsAlive)
            {
                playerState.RespawnTimeLeft -= Time.deltaTime;

                if (playerState.RespawnTimeLeft <= 0)
                {
                    ServerSpawnPlayer(server, playerState);
                }
            }

            var wasReloadingBeforeUpdate = playerState.IsReloading;

            OsFps.Instance.UpdatePlayer(playerState);

            if (wasReloadingBeforeUpdate && (playerState.ReloadTimeLeft <= 0))
            {
                server.PlayerFinishReload(playerState);
            }

            // kill if too low in map
            if (playerState.Position.y <= OsFps.KillPlaneY)
            {
                ServerDamagePlayer(server, playerState, 9999, null);
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