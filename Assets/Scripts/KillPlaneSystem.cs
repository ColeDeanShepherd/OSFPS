using System.Linq;
using UnityEngine;
using Unity.Entities;

public class KillPlaneSystem : ComponentSystem
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
            var playerState = server.CurrentGameState.Players.First(ps => ps.Id == entity.PlayerComponent.Id);

            // kill if too low in map
            if (playerState.Position.y <= OsFps.KillPlaneY)
            {
                PlayerSystem.Instance.ServerDamagePlayer(server, playerState, 9999, null);
            }
        }
    }
}