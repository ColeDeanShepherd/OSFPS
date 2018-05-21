using Unity.Entities;
using UnityEngine;

public class PlayerRespawnSystem : ComponentSystem
{
    public struct Group
    {
        public PlayerComponent PlayerComponent;
    }

    protected override void OnUpdate()
    {
        var server = OsFps.Instance.Server;
        if (server != null)
        {
            ServerOnUpdate(server);
        }
    }

    private void ServerOnUpdate(Server server)
    {
        foreach (var entity in GetEntities<Group>())
        {
            var playerState = entity.PlayerComponent.State;
            if (playerState.RespawnTimeLeft > 0)
            {
                playerState.RespawnTimeLeft -= Time.deltaTime;

                if (playerState.RespawnTimeLeft <= 0)
                {
                    PlayerSystem.Instance.ServerSpawnPlayer(server, playerState.Id);
                }
            }
        }
    }
}