using Unity.Entities;
using UnityEngine;

public class KillPlaneSystem : ComponentSystem
{
    public struct Data
    {
        public int Length;
        public ComponentArray<Transform> Transform;
    }

    protected override void OnUpdate()
    {
        var server = OsFps.Instance?.Server;
        if (server != null)
        {
            ServerOnUpdate(server);
        }
    }

    [Inject] private Data data;

    private void ServerOnUpdate(Server server)
    {
        for (var i = 0; i < data.Length; i++)
        {
            if (data.Transform[i].position.y <= OsFps.KillPlaneY)
            {
                var playerObjectComponent = data.Transform[i].gameObject.GetComponent<PlayerObjectComponent>();

                if (playerObjectComponent == null)
                {
                    Object.Destroy(data.Transform[i].gameObject);
                }
                else
                {
                    PlayerObjectSystem.Instance.ServerDamagePlayer(server, playerObjectComponent, 9999, null);
                }
            }
        }
    }
}