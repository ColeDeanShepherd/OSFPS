using System.Linq;
using UnityEngine;

public class GrenadeSystem
{
    public void OnUpdate()
    {
        var server = OsFps.Instance.Server;
        if (server != null)
        {
            ServerOnUpdate(server);
        }
    }
    
    private void ServerOnUpdate(Server server)
    {
        var deltaTime = Time.deltaTime;

        foreach (var grenade in server.CurrentGameState.Grenades)
        {
            if (grenade.IsFuseBurning)
            {
                if (grenade.TimeUntilDetonation > 0)
                {
                    grenade.TimeUntilDetonation -= deltaTime;
                }
            }
        }

        var grenadesToDetonate = server.CurrentGameState.Grenades
            .Where(gs => gs.IsFuseBurning && (gs.TimeUntilDetonation <= 0))
            .ToList();
        foreach (var grenadeToDetonate in grenadesToDetonate)
        {
            ServerDetonateGrenade(server, grenadeToDetonate);
        }
    }

    private void ServerDetonateGrenade(Server server, GrenadeState grenade)
    {
        var grenadeComponent = OsFps.Instance.FindGrenade(grenade.Id);
        var grenadeDefinition = OsFps.GetGrenadeDefinitionByType(grenade.Type);
        var grenadePosition = grenadeComponent.transform.position;

        // apply damage & forces to players within range
        var affectedColliders = Physics.OverlapSphere(
            grenadePosition, grenadeDefinition.ExplosionRadius
        );

        foreach (var collider in affectedColliders)
        {
            // Apply damage.
            var playerComponent = collider.gameObject.GetComponent<PlayerComponent>();
            if (playerComponent != null)
            {
                var playerState = server.CurrentGameState.Players.FirstOrDefault(ps => ps.Id == playerComponent.Id);
                var closestPointToGrenade = collider.ClosestPoint(grenadePosition);
                var distanceFromGrenade = Vector3.Distance(closestPointToGrenade, grenadePosition);
                var unclampedDamagePercent = (grenadeDefinition.ExplosionRadius - distanceFromGrenade) / grenadeDefinition.ExplosionRadius;
                var damagePercent = Mathf.Max(unclampedDamagePercent, 0);
                var damage = damagePercent * grenadeDefinition.Damage;
                server.playerSystem.ServerDamagePlayer(server, playerState, (int)damage, null);
            }

            // Apply forces.
            var rigidbody = collider.gameObject.GetComponent<Rigidbody>();
            if (rigidbody != null)
            {
                rigidbody.AddExplosionForce(
                    OsFps.GrenadeExplosionForce, grenadePosition, grenadeDefinition.ExplosionRadius
                );
            }
        }

        // destroy grenade object
        Object.Destroy(grenadeComponent.gameObject);

        // remove grenade state
        server.CurrentGameState.Grenades.RemoveAll(gs => gs.Id == grenade.Id);

        // send message
        var message = new DetonateGrenadeMessage
        {
            Id = grenade.Id,
            Position = grenadePosition,
            Type = grenade.Type
        };
        server.SendMessageToAllClients(server.reliableChannelId, message);
    }
}