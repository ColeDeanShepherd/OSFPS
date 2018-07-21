using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

public class RemoveDeadPlayerSystem : ComponentSystem
{
    public struct Data
    {
        public PlayerObjectComponent PlayerObjectComponent;
    }

    public static RemoveDeadPlayerSystem Instance;

    public RemoveDeadPlayerSystem()
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
        var playerObjectsToDestroy = new List<GameObject>();

        foreach (var entity in GetEntities<Data>())
        {
            if (!entity.PlayerObjectComponent.State.IsAlive)
            {
                playerObjectsToDestroy.Add(entity.PlayerObjectComponent.gameObject);
            }
        }

        foreach (var playerObjectToDestroy in playerObjectsToDestroy)
        {
            Object.Destroy(playerObjectToDestroy);
        }
    }
}