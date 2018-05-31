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
            Grenades = GetGrenadeStates(),
            GrenadeSpawners = GetGrenadeSpawnerStates(),
            Rockets = GetRocketStates()
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
                gc.State.RigidBodyState = (gc.Rigidbody != null)
                    ? ToRigidBodyState(gc.Rigidbody)
                    : new RigidBodyState();
                return gc.State;
            })
            .Where(gs => gs != null)
            .ToList();
    }
    public List<GrenadeSpawnerState> GetGrenadeSpawnerStates()
    {
        return Object.FindObjectsOfType<GrenadeSpawnerComponent>()
            .Select(wsc => wsc.State)
            .Where(wss => wss != null)
            .ToList();
    }
    public List<RocketState> GetRocketStates()
    {
        return Object.FindObjectsOfType<RocketComponent>()
            .Select(rc =>
            {
                rc.State.RigidBodyState = (rc.Rigidbody != null)
                    ? ToRigidBodyState(rc.Rigidbody)
                    : new RigidBodyState();
                return rc.State;
            })
            .Where(rs => rs != null)
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
}