using UnityEngine;
using Unity.Entities;
using System.Linq;
using System.Collections.Generic;

public class GrenadeSpawnerSystem : ComponentSystem
{
    public struct Data
    {
        public int Length;
        public ComponentArray<GrenadeSpawnerComponent> GrenadeSpawnerComponent;
    }

    public static GrenadeSpawnerSystem Instance;

    public GrenadeSpawnerSystem()
    {
        Instance = this;
    }
    public void ServerSpawnGrenade(Server server, GrenadeSpawnerState grenadeSpawnerState)
    {
        if (grenadeSpawnerState.TimeUntilNextSpawn > 0) return;

        var weaponDefinition = GrenadeSystem.Instance.GetGrenadeDefinitionByType(grenadeSpawnerState.Type);
        var weaponSpawnerComponent = FindGrenadeSpawnerComponent(grenadeSpawnerState.Id);

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
        SpawnLocalGrenadeObject(weaponObjectState);
    }

    public GrenadeSpawnerComponent FindGrenadeSpawnerComponent(uint id)
    {
        return Object.FindObjectsOfType<GrenadeSpawnerComponent>()
            .FirstOrDefault(gsc => gsc.State.Id == id);
    }
    public GameObject SpawnLocalGrenadeObject(GrenadeState grenadeState)
    {
        var grenadePrefab = GrenadeSystem.Instance.GetGrenadeDefinitionByType(grenadeState.Type).Prefab;
        var grenadeObject = GameObject.Instantiate(
            grenadePrefab,
            grenadeState.RigidBodyState.Position,
            Quaternion.Euler(grenadeState.RigidBodyState.EulerAngles)
        );

        var grenadeComponent = grenadeObject.GetComponent<GrenadeComponent>();
        grenadeComponent.State = grenadeState;

        var rigidbody = grenadeComponent.Rigidbody;
        rigidbody.velocity = grenadeState.RigidBodyState.Velocity;
        rigidbody.angularVelocity = grenadeState.RigidBodyState.AngularVelocity;

        return grenadeObject;
    }

    protected override void OnUpdate()
    {
        var server = OsFps.Instance?.Server;
        if (server != null)
        {
            ServerOnUpdate(server);
        }
    }

    [Inject] private Data data;

    private void ServerOnUpdate(Server server)
    {
        for (var i = 0; i < data.Length; i++)
        {
            var grenadeSpawner = data.GrenadeSpawnerComponent[i].State;

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
}