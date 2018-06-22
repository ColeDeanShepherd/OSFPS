using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class GameStateScraperSystem
{
    public GameState GetGameState()
    {
        return new GameState
        {
            SequenceNumber = GenerateSequenceNumber(),
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
            .Select(poc => poc.State)
            .Where(pos => pos != null)
            .ToList();
    }
    public List<WeaponObjectState> GetWeaponObjectStates()
    {
        return Object.FindObjectsOfType<WeaponComponent>()
            .Select(wc => wc.State)
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
            .Select(gc => gc.State)
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
            .Select(rc => rc.State)
            .Where(rs => rs != null)
            .ToList();
    }

    public static RigidBodyState ToRigidBodyState(Rigidbody rigidbody)
    {
        return new RigidBodyState
        {
            Position = rigidbody.transform.position,
            EulerAngles = rigidbody.transform.eulerAngles,
            Velocity = rigidbody.velocity,
            AngularVelocity = rigidbody.angularVelocity
        };
    }

    private uint _nextSequenceNumber = 1;
    private uint GenerateSequenceNumber()
    {
        var generatedSequenceNumber = _nextSequenceNumber;
        _nextSequenceNumber++;
        return generatedSequenceNumber;
    }
}