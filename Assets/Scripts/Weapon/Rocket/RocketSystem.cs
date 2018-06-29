using UnityEngine;
using Unity.Entities;
using System.Linq;
using Unity.Mathematics;

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
        var rocketPosition = (float3)rocketComponent.transform.position;

        // apply damage & forces to players within range
        var rocketLauncherDefinition = WeaponSystem.Instance.GetWeaponDefinitionByType(WeaponType.RocketLauncher);
        OsFps.Instance.ApplyExplosionDamageAndForces(
            server, rocketPosition, OsFps.RocketExplosionRadius, OsFps.RocketExplosionForce,
            rocketLauncherDefinition.DamagePerBullet, rocketComponent.State.ShooterPlayerId
        );

        // destroy rocket object
        Object.Destroy(rocketComponent.gameObject);

        // send message
        server.ServerPeer.CallRpcOnAllClients("ClientOnDetonateRocket", server.reliableChannelId, new
        {
            id = rocketComponent.State.Id,
            position = rocketPosition
        });
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
}