using Unity.Entities;

public class KillPlaneSystem : ComponentSystem
{
    public struct Group
    {
        public PlayerObjectComponent PlayerObjectComponent;
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
            var playerObjectComponent = entity.PlayerObjectComponent;

            // kill if too low in map
            if (playerObjectComponent.transform.position.y <= OsFps.KillPlaneY)
            {
                PlayerSystem.Instance.ServerDamagePlayer(server, playerObjectComponent, 9999, null);
            }
        }
    }
}