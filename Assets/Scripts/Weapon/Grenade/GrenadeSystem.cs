using UnityEngine;
using Unity.Entities;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;

public class GrenadeSystem : ComponentSystem
{
    public struct Data
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
        var server = OsFps.Instance?.Server;
        if (server != null)
        {
            ServerOnUpdate(server);
        }
    }

    private void ServerOnUpdate(Server server)
    {
        var deltaTime = Time.deltaTime;

        foreach (var entity in GetEntities<Data>())
        {
            var grenade = entity.GrenadeComponent.State;

            if (grenade.TimeUntilDetonation > 0)
            {
                grenade.TimeUntilDetonation -= deltaTime;
            }
        }

        var grenadesToDetonate = new List<GrenadeComponent>();
        foreach (var entity in GetEntities<Data>())
        {
            var grenade = entity.GrenadeComponent.State;

            if (grenade.TimeUntilDetonation <= 0)
            {
                grenadesToDetonate.Add(entity.GrenadeComponent);
            }
        }

        foreach (var grenadeToDetonate in grenadesToDetonate)
        {
            ServerDetonateGrenade(server, grenadeToDetonate);
        }
    }
    private void ServerDetonateGrenade(Server server, GrenadeComponent grenadeComponent)
    {
        var grenade = grenadeComponent.State;
        var grenadeDefinition = GetGrenadeDefinitionByType(grenade.Type);
        var grenadePosition = (float3)grenadeComponent.transform.position;

        // apply damage & forces to players within range
        WeaponSystem.Instance.ApplyExplosionDamageAndForces(
            server, grenadePosition, grenadeDefinition.ExplosionRadius, OsFps.GrenadeExplosionForce,
            grenadeDefinition.Damage, grenade.ThrowerPlayerId
        );

        // destroy grenade object
        Object.Destroy(grenadeComponent.gameObject);

        // send message
        server.ServerPeer.CallRpcOnAllClients("ClientOnDetonateGrenade", server.ServerPeer.reliableChannelId, new
        {
            id = grenade.Id,
            position = grenadePosition,
            type = grenade.Type
        });
    }

    public void GrenadeOnCollisionEnter(GrenadeComponent grenadeComponent, Collision collision)
    {
        var grenadeState = grenadeComponent.State;

        if (grenadeState.IsActive)
        {
            if (OsFps.Instance.IsServer)
            {
                grenadeState.TimeUntilDetonation = GetGrenadeDefinitionByType(grenadeState.Type).TimeAfterHitUntilDetonation;
            }

            if (grenadeComponent.State.Type == GrenadeType.Fragmentation)
            {
                if (OsFps.Instance.IsClient)
                {
                    var audioSource = grenadeComponent.GetComponent<AudioSource>();
                    audioSource.PlayOneShot(OsFps.Instance.FragGrenadeBounceSound);
                }
            }
            else if (grenadeComponent.State.Type == GrenadeType.Sticky)
            {
                var contactPoint = collision.contacts[0].point;
                StickStickyGrenadeToObject(grenadeComponent, collision.gameObject, contactPoint);
            }
        }
    }
    public void StickStickyGrenadeToObject(GrenadeComponent grenadeComponent, GameObject hitObject, Vector3 contactPoint)
    {
        grenadeComponent.transform.position = contactPoint;
        grenadeComponent.Rigidbody.isKinematic = true;
        grenadeComponent.Collider.isTrigger = true;
        grenadeComponent.transform.SetParent(hitObject.transform);
    }

    public void GrenadeOnCollisionStay(GrenadeComponent grenadeComponent, Collision collision)
    {
        var otherGameObject = collision.gameObject;
        var playerObject = otherGameObject.FindObjectOrAncestorWithTag(OsFps.PlayerTag);

        if (playerObject != null)
        {
            PlayerObjectSystem.Instance.OnPlayerCollidingWithGrenade(playerObject, grenadeComponent.gameObject);
        }
    }

    public void GrenadeOnDestroy(GrenadeComponent grenadeComponent)
    {
        if (grenadeComponent.State.GrenadeSpawnerId.HasValue)
        {
            var grenadeSpawnerComponent = GrenadeSpawnerSystem.Instance.FindGrenadeSpawnerComponent(
                grenadeComponent.State.GrenadeSpawnerId.Value
            );

            if (grenadeSpawnerComponent != null)
            {
                grenadeSpawnerComponent.State.TimeUntilNextSpawn = GetGrenadeDefinitionByType(
                    grenadeSpawnerComponent.State.Type
                ).SpawnInterval;
            }
        }
    }

    public GrenadeComponent FindGrenadeComponent(uint id)
    {
        return Object.FindObjectsOfType<GrenadeComponent>()
           .FirstOrDefault(g => g.State.Id == id);
    }
    public GrenadeDefinition GetGrenadeDefinitionByType(GrenadeType type)
    {
        return OsFps.Instance.GrenadeDefinitionComponents
            .FirstOrDefault(gdc => gdc.Definition.Type == type)
            ?.Definition;
    }
}