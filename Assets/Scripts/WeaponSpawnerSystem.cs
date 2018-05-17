using UnityEngine;

public class WeaponSpawnerSystem
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
        foreach (var weaponSpawner in server.CurrentGameState.WeaponSpawners)
        {
            // shot interval
            if (weaponSpawner.TimeUntilNextSpawn > 0)
            {
                weaponSpawner.TimeUntilNextSpawn -= Time.deltaTime;
            }

            if (weaponSpawner.TimeUntilNextSpawn <= 0)
            {
                ServerSpawnWeapon(server, weaponSpawner);
                weaponSpawner.TimeUntilNextSpawn += OsFps.GetWeaponDefinitionByType(weaponSpawner.Type).SpawnInterval;
            }
        }
    }
    public void ServerSpawnWeapon(Server server, WeaponSpawnerState weaponSpawnerState)
    {
        if (weaponSpawnerState.TimeUntilNextSpawn > 0) return;

        var weaponDefinition = OsFps.GetWeaponDefinitionByType(weaponSpawnerState.Type);
        var bulletsLeft = weaponDefinition.MaxAmmo / 2;
        var bulletsLeftInMagazine = Mathf.Min(weaponDefinition.BulletsPerMagazine, bulletsLeft);
        var weaponSpawnerComponent = OsFps.Instance.FindWeaponSpawnerComponent(weaponSpawnerState.Id);

        var weaponObjectState = new WeaponObjectState
        {
            Id = server.GenerateNetworkId(),
            Type = weaponSpawnerState.Type,
            BulletsLeftInMagazine = (ushort)bulletsLeftInMagazine,
            BulletsLeftOutOfMagazine = (ushort)(bulletsLeft - bulletsLeftInMagazine),
            RigidBodyState = new RigidBodyState
            {
                Position = weaponSpawnerComponent.transform.position,
                EulerAngles = weaponSpawnerComponent.transform.eulerAngles,
                Velocity = Vector3.zero,
                AngularVelocity = Vector3.zero
            }
        };
        var weaponObject = OsFps.Instance.SpawnLocalWeaponObject(weaponObjectState);

        var weaponComponent = weaponObject.GetComponent<WeaponComponent>();
        server.CurrentGameState.WeaponObjects.Add(weaponObjectState);
    }
}