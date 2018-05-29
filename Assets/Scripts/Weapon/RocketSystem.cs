using System.Linq;
using UnityEngine;
using Unity.Entities;
using System.Collections.Generic;

public class RocketSystem : ComponentSystem
{
    public struct Group
    {
        public RocketComponent RocketComponent;
    }

    public static RocketSystem Instance;

    public RocketSystem()
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
        var deltaTime = Time.deltaTime;
    }
    private void ServerDetonateRocket(Server server, RocketComponent rocketComponent)
    {
        var rocketPosition = rocketComponent.transform.position;

        // apply damage & forces to players within range
        var affectedColliders = Physics.OverlapSphere(
            rocketPosition, OsFps.RocketExplosionRadius
        );

        foreach (var collider in affectedColliders)
        {
            // Apply damage.
            var playerObjectComponent = collider.gameObject.FindComponentInObjectOrAncestor<PlayerObjectComponent>();
            if (playerObjectComponent != null)
            {
                var playerObjectState = playerObjectComponent.State;
                var closestPointToRocket = collider.ClosestPoint(rocketPosition);
                var distanceFromRocket = Vector3.Distance(closestPointToRocket, rocketPosition);
                var unclampedDamagePercent = (OsFps.RocketExplosionRadius - distanceFromRocket) / OsFps.RocketExplosionRadius;
                var damagePercent = Mathf.Max(unclampedDamagePercent, 0);
                var damage = damagePercent * OsFps.RocketLauncherDefinition.DamagePerBullet;

                // TODO: don't call system directly
                var attackingPlayerObjectComponent = rocketComponent.State.ShooterPlayerId.HasValue
                    ? OsFps.Instance.FindPlayerObjectComponent(rocketComponent.State.ShooterPlayerId.Value)
                    : null;
                PlayerSystem.Instance.ServerDamagePlayer(
                    server, playerObjectComponent, damage, attackingPlayerObjectComponent
                );
            }

            // Apply forces.
            var rigidbody = collider.gameObject.GetComponent<Rigidbody>();
            if (rigidbody != null)
            {
                rigidbody.AddExplosionForce(
                    OsFps.RocketExplosionForce, rocketPosition, OsFps.RocketExplosionRadius
                );
            }
        }

        // destroy rocket object
        Object.Destroy(rocketComponent.gameObject);

        // send message
        OsFps.Instance.CallRpcOnAllClients("ClientOnDetonateRocket", server.reliableChannelId, new
        {
            id = rocketComponent.State.Id,
            position = rocketPosition
        });
    }

    public void ServerRocketOnCollisionEnter(Server server, RocketComponent rocketComponent, Collision collision)
    {
        ServerDetonateRocket(server, rocketComponent);
    }
}