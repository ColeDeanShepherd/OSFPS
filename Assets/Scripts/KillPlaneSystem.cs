using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

public class KillPlaneSystem : ComponentSystem
{
    public struct Data
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
        var gameObjectsToDestroy = new List<GameObject>();
        var playerObjectComponentsToDestroy = new List<PlayerObjectComponent>();

        foreach (var entity in GetEntities<Data>())
        {
            if (entity.Transform.position.y <= OsFps.KillPlaneY)
            {
                var playerObjectComponent = entity.Transform.gameObject.GetComponent<PlayerObjectComponent>();

                if (playerObjectComponent == null)
                {
                    gameObjectsToDestroy.Add(entity.Transform.gameObject);
                }
                else
                {
                    playerObjectComponentsToDestroy.Add(playerObjectComponent);
                }
            }
        }

        foreach (var gameObjectToDestroy in gameObjectsToDestroy)
        {
            Object.Destroy(gameObjectToDestroy);
        }

        foreach (var playerObjectComponentToDestroy in playerObjectComponentsToDestroy)
        {
            PlayerObjectSystem.Instance.ServerDamagePlayer(server, playerObjectComponentToDestroy, 9999, null);
        }
    }
}