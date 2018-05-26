using System.Linq;
using UnityEngine;
using Unity.Entities;
using System.Collections.Generic;

public class GrenadeSystem : ComponentSystem
{
    public struct Group
    {
        public GrenadeComponent GrenadeComponent;
    }

    public static GrenadeSystem Instance;

    public GrenadeSystem()
    {
        Instance = this;
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
        var deltaTime = Time.deltaTime;

        var entities = GetEntities<Group>();
        foreach (var entity in entities)
        {
            var grenade = entity.GrenadeComponent.State;

            if (grenade.IsFuseBurning)
            {
                if (grenade.TimeUntilDetonation > 0)
                {
                    grenade.TimeUntilDetonation -= deltaTime;
                }
            }
        }

        var grenadesToDetonate = new List<GrenadeState>();
        foreach (var entity in entities)
        {
            var grenade = entity.GrenadeComponent.State;

            if (grenade.IsFuseBurning && (grenade.TimeUntilDetonation <= 0))
            {
                grenadesToDetonate.Add(grenade);
            }
        }

        foreach (var grenadeToDetonate in grenadesToDetonate)
        {
            ServerDetonateGrenade(server, grenadeToDetonate);
        }
    }
    private void ServerDetonateGrenade(Server server, GrenadeState grenade)
    {
        var grenadeComponent = OsFps.Instance.FindGrenadeComponent(grenade.Id);
        var grenadeDefinition = OsFps.GetGrenadeDefinitionByType(grenade.Type);
        var grenadePosition = grenadeComponent.transform.position;

        // apply damage & forces to players within range
        var affectedColliders = Physics.OverlapSphere(
            grenadePosition, grenadeDefinition.ExplosionRadius
        );

        foreach (var collider in affectedColliders)
        {
            // Apply damage.
            var playerObjectComponent = collider.gameObject.GetComponent<PlayerObjectComponent>();
            if (playerObjectComponent != null)
            {
                var playerObjectState = playerObjectComponent.State;
                var closestPointToGrenade = collider.ClosestPoint(grenadePosition);
                var distanceFromGrenade = Vector3.Distance(closestPointToGrenade, grenadePosition);
                var unclampedDamagePercent = (grenadeDefinition.ExplosionRadius - distanceFromGrenade) / grenadeDefinition.ExplosionRadius;
                var damagePercent = Mathf.Max(unclampedDamagePercent, 0);
                var damage = damagePercent * grenadeDefinition.Damage;

                // TODO: don't call system directly
                PlayerSystem.Instance.ServerDamagePlayer(server, playerObjectComponent, damage, null);
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

        // send message
        OsFps.Instance.CallRpcOnAllClients("ClientOnDetonateGrenade", server.reliableChannelId, new
        {
            id = grenade.Id,
            position = grenadePosition,
            type = grenade.Type
        });
    }

    public void ServerPlayerThrowGrenade(Server server, PlayerObjectComponent playerObjectComponent)
    {
        var playerObjectState = playerObjectComponent.State;
        if (!playerObjectState.CanThrowGrenade) return;

        var cameraPosition = playerObjectComponent.CameraPointObject.transform.position;
        var cameraForward = playerObjectComponent.CameraPointObject.transform.forward;
        var throwRay = new Ray(cameraPosition + (0.5f * cameraForward), cameraForward);
        var currentGrenadeSlot = playerObjectState.GrenadeSlots[playerObjectState.CurrentGrenadeSlotIndex];

        var grenadeState = new GrenadeState
        {
            Id = server.GenerateNetworkId(),
            Type = currentGrenadeSlot.GrenadeType,
            IsFuseBurning = false,
            TimeUntilDetonation = OsFps.GetGrenadeDefinitionByType(currentGrenadeSlot.GrenadeType).TimeAfterHitUntilDetonation,
            RigidBodyState = new RigidBodyState
            {
                Position = throwRay.origin,
                EulerAngles = Quaternion.LookRotation(throwRay.direction, Vector3.up).eulerAngles,
                Velocity = OsFps.GrenadeThrowSpeed * throwRay.direction,
                AngularVelocity = Vector3.zero
            }
        };
        var grenadeObject = OsFps.Instance.SpawnLocalGrenadeObject(grenadeState);
        grenadeObject.GetComponent<GrenadeComponent>();

        currentGrenadeSlot.GrenadeCount--;
        playerObjectState.TimeUntilCanThrowGrenade = OsFps.GrenadeThrowInterval;
    }
    public void ServerGrenadeOnCollisionEnter(Server server, GrenadeComponent grenadeComponent, Collision collision)
    {
        Debug.Log("Grenade collided with " + collision.gameObject.name);

        var grenadeState = grenadeComponent.State;
        grenadeState.IsFuseBurning = true;
    }
}