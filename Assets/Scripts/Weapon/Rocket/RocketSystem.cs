using UnityEngine;
using Unity.Entities;
using System.Linq;
using Unity.Mathematics;
using System.Collections.Generic;

public class RocketSystem : ComponentSystem
{
    public struct Data
    {
        public RocketComponent RocketComponent;
    }

    public static RocketSystem Instance;

    public RocketSystem()
    {
        Instance = this;
    }

    public void ServerRocketOnCollisionEnter(Server server, RocketComponent rocketComponent, Collision collision)
    {
        ServerDetonateRocket(server, rocketComponent);
    }

    public GameObject SpawnLocalRocketObject(RocketState rocketState)
    {
        var rocketObject = GameObject.Instantiate(
            OsFps.Instance.RocketPrefab,
            rocketState.RigidBodyState.Position,
            Quaternion.Euler(rocketState.RigidBodyState.EulerAngles)
        );

        var rocketComponent = rocketObject.GetComponent<RocketComponent>();
        rocketComponent.State = rocketState;

        var rigidbody = rocketComponent.Rigidbody;
        rigidbody.velocity = rocketState.RigidBodyState.Velocity;
        rigidbody.angularVelocity = rocketState.RigidBodyState.AngularVelocity;

        return rocketObject;
    }
    public void RocketOnCollisionEnter(RocketComponent rocketComponent, Collision collision)
    {
        if (OsFps.Instance.Server != null)
        {
            RocketSystem.Instance.ServerRocketOnCollisionEnter(OsFps.Instance.Server, rocketComponent, collision);
        }
    }

    public RocketComponent FindRocketComponent(uint id)
    {
        return Object.FindObjectsOfType<RocketComponent>()
           .FirstOrDefault(g => g.State.Id == id);
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
    }

    private void ServerDetonateRocket(Server server, RocketComponent rocketComponent)
    {
        var rocketPosition = (float3)rocketComponent.transform.position;

        // apply damage & forces to players within range
        var rocketLauncherDefinition = WeaponSystem.Instance.GetWeaponDefinitionByType(WeaponType.RocketLauncher);
        WeaponSystem.Instance.ApplyExplosionDamageAndForces(
            server, rocketPosition, OsFps.RocketExplosionRadius, OsFps.RocketExplosionForce,
            rocketLauncherDefinition.DamagePerBullet, rocketComponent.State.ShooterPlayerId
        );

        // destroy rocket object
        Object.Destroy(rocketComponent.gameObject);

        // send message
        server.ServerPeer.CallRpcOnAllClients("ClientOnDetonateRocket", server.ServerPeer.reliableChannelId, new
        {
            id = rocketComponent.State.Id,
            position = rocketPosition
        });
    }
}