using UnityEngine;
using Unity.Entities;

public class GrenadeSpawnerSystem : ComponentSystem
{
    public struct Group
    {
        public GrenadeSpawnerComponent GrenadeSpawnerComponent;
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
        foreach (var entity in GetEntities<Group>())
        {
            var grenadeSpawner = entity.GrenadeSpawnerComponent.State;

            // spawn interval
            if (grenadeSpawner.TimeUntilNextSpawn > 0)
            {
                grenadeSpawner.TimeUntilNextSpawn -= Time.deltaTime;
            }

            if (grenadeSpawner.TimeUntilNextSpawn <= 0)
            {
                ServerSpawnGrenade(server, grenadeSpawner);
                grenadeSpawner.TimeUntilNextSpawn = null;
            }
        }
    }
    public void ServerSpawnGrenade(Server server, GrenadeSpawnerState grenadeSpawnerState)
    {
        if (grenadeSpawnerState.TimeUntilNextSpawn > 0) return;

        var weaponDefinition = OsFps.Instance.GetGrenadeDefinitionByType(grenadeSpawnerState.Type);
        var weaponSpawnerComponent = OsFps.Instance.FindGrenadeSpawnerComponent(grenadeSpawnerState.Id);

        var weaponObjectState = new GrenadeState
        {
            Id = server.GenerateNetworkId(),
            Type = grenadeSpawnerState.Type,
            RigidBodyState = new RigidBodyState
            {
                Position = weaponSpawnerComponent.transform.position,
                EulerAngles = weaponSpawnerComponent.transform.eulerAngles,
                Velocity = Vector3.zero,
                AngularVelocity = Vector3.zero
            },
            IsActive = false,
            GrenadeSpawnerId = grenadeSpawnerState.Id
        };
        OsFps.Instance.SpawnLocalGrenadeObject(weaponObjectState);
    }
}