using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class GameStateScraperSystem
{
    public GameState GetGameState()
    {
        return new GameState
        {
            Players = GetPlayerStates(),
            PlayerObjects = GetPlayerObjectStates(),
            WeaponObjects = GetWeaponObjectStates(),
            WeaponSpawners = GetWeaponSpawnerStates(),
            Grenades = GetGrenadeStates()
        };
    }
    public List<PlayerState> GetPlayerStates()
    {
        return Object.FindObjectsOfType<PlayerComponent>()
            .Select(pc => pc.State)
            .Where(ps => ps != null)
            .ToList();
    }
    public List<PlayerObjectState> GetPlayerObjectStates()
    {
        return Object.FindObjectsOfType<PlayerObjectComponent>()
            .Select(poc =>
            {
                poc.State.Position = poc.transform.position;
                poc.State.Velocity = poc.Rigidbody.velocity;
                return poc.State;
            })
            .Where(pos => pos != null)
            .ToList();
    }
    public List<WeaponObjectState> GetWeaponObjectStates()
    {
        return Object.FindObjectsOfType<WeaponComponent>()
            .Select(wc =>
            {
                if (wc.State != null)
                {
                    wc.State.RigidBodyState = (wc.Rigidbody != null)
                        ? ToRigidBodyState(wc.Rigidbody)
                        : new RigidBodyState();
                }

                return wc.State;
            })
            .Where(wos => wos != null)
            .ToList();
    }
    public List<WeaponSpawnerState> GetWeaponSpawnerStates()
    {
        return Object.FindObjectsOfType<WeaponSpawnerComponent>()
            .Select(wsc => wsc.State)
            .Where(wss => wss != null)
            .ToList();
    }
    public List<GrenadeState> GetGrenadeStates()
    {
        return Object.FindObjectsOfType<GrenadeComponent>()
            .Select(gc =>
            {
                gc.State.RigidBodyState = ToRigidBodyState(gc.Rigidbody);
                return gc.State;
            })
            .Where(gs => gs != null)
            .ToList();
    }

    public RigidBodyState ToRigidBodyState(Rigidbody rigidbody)
    {
        return new RigidBodyState
        {
            Position = rigidbody.transform.position,
            EulerAngles = rigidbody.transform.eulerAngles,
            Velocity = rigidbody.velocity,
            AngularVelocity = rigidbody.angularVelocity
        };
    }

    private WeaponObjectState ToWeaponObjectState(uint weaponObjectId, WeaponComponent weaponComponent)
    {
        return new WeaponObjectState
        {
            Id = weaponObjectId,
            Type = weaponComponent.Type,
            BulletsLeftInMagazine = weaponComponent.BulletsLeftInMagazine,
            BulletsLeftOutOfMagazine = weaponComponent.BulletsLeftOutOfMagazine,
            RigidBodyState = ToRigidBodyState(weaponComponent.Rigidbody)
        };
    }
    public List<WeaponObjectState> ServerInitWeaponObjectStatesInGameObjects(Server server)
    {
        var weaponObjectStates = new List<WeaponObjectState>();

        var weaponComponents = Object.FindObjectsOfType<WeaponComponent>();
        foreach (var weaponComponent in weaponComponents)
        {
            var weaponObjectState = ToWeaponObjectState(server.GenerateNetworkId(), weaponComponent);
            weaponComponent.State = weaponObjectState;

            weaponObjectStates.Add(weaponObjectState);
        }

        return weaponObjectStates;
    }

    private WeaponSpawnerState ToWeaponSpawnerState(uint weaponSpawnerId, WeaponSpawnerComponent weaponSpawnerComponent)
    {
        var state = new WeaponSpawnerState
        {
            Id = weaponSpawnerId,
            Type = weaponSpawnerComponent.WeaponType,
            TimeUntilNextSpawn = 0
        };

        return state;
    }
    public List<WeaponSpawnerState> ServerInitWeaponSpawnerStatesInGameObjects(Server server)
    {
        var weaponSpawnerStates = new List<WeaponSpawnerState>();

        var weaponSpawnerComponents = Object.FindObjectsOfType<WeaponSpawnerComponent>();
        foreach (var weaponSpawnerComponent in weaponSpawnerComponents)
        {
            var weaponSpawnerState = ToWeaponSpawnerState(server.GenerateNetworkId(), weaponSpawnerComponent);
            weaponSpawnerComponent.State = weaponSpawnerState;

            weaponSpawnerStates.Add(weaponSpawnerState);
        }

        return weaponSpawnerStates;
    }
}