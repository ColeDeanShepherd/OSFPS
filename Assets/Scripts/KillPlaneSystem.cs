using Unity.Entities;
using UnityEngine;

public class KillPlaneSystem : ComponentSystem
{
    public struct Group
    {
        public Transform Transform;
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
        foreach (var entity in GetEntities<Group>())
        {
            if (entity.Transform.position.y <= OsFps.KillPlaneY)
            {
                var playerObjectComponent = entity.Transform.gameObject.GetComponent<PlayerObjectComponent>();

                if (playerObjectComponent == null)
                {
                    Object.Destroy(entity.Transform.gameObject);
                }
                else
                {
                    PlayerSystem.Instance.ServerDamagePlayer(server, playerObjectComponent, 9999, null);
                }
            }
        }
    }
}