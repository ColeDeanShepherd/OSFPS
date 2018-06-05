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
        var rocketLauncherDefinition = OsFps.Instance.GetWeaponDefinitionByType(WeaponType.RocketLauncher);
        OsFps.Instance.ApplyExplosionDamageAndForces(
            server, rocketPosition, OsFps.RocketExplosionRadius, OsFps.RocketExplosionForce,
            rocketLauncherDefinition.DamagePerBullet, rocketComponent.State.ShooterPlayerId
        );

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