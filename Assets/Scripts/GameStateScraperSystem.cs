using System.Collections.Generic;
using UnityEngine;

public class GameStateScraperSystem
{
    public void OnLateUpdate()
    {
        var server = OsFps.Instance.Server;
        if (server != null)
        {
            ServerOnLateUpdate(server);
        }
    }

    public void ServerOnLateUpdate(Server server)
    {
        ServerUpdateGameStateFromObjects(server);
    }
    private void ServerUpdateGameStateFromObjects(Server server)
    {
        foreach (var playerState in server.CurrentGameState.Players)
        {
            var playerComponent = OsFps.Instance.FindPlayerComponent(playerState.Id);

            if (playerComponent != null)
            {
                playerState.Position = playerComponent.transform.position;
                playerState.Velocity = playerComponent.Rigidbody.velocity;
                playerState.LookDirAngles = OsFps.Instance.GetPlayerLookDirAngles(playerComponent);
            }
        }

        foreach (var weaponObjectState in server.CurrentGameState.WeaponObjects)
        {
            var weaponObject = OsFps.Instance.FindWeaponObject(weaponObjectState.Id);

            if (weaponObject != null)
            {
                var weaponComponent = weaponObject.GetComponent<WeaponComponent>();
                ServerUpdateRigidBodyStateFromRigidBody(weaponObjectState.RigidBodyState, weaponComponent.Rigidbody);
            }
            else
            {
                // remove weapon object state???
            }
        }

        foreach (var grenadeState in server.CurrentGameState.Grenades)
        {
            var grenadeObject = OsFps.Instance.FindGrenade(grenadeState.Id);

            if (grenadeObject != null)
            {
                var grenadeComponent = grenadeObject.GetComponent<GrenadeComponent>();
                ServerUpdateRigidBodyStateFromRigidBody(grenadeState.RigidBodyState, grenadeComponent.Rigidbody);
            }
            else
            {
                // remove weapon object state???
            }
        }
    }
    private void ServerUpdateRigidBodyStateFromRigidBody(RigidBodyState rigidBodyState, Rigidbody rigidbody)
    {
        rigidBodyState.Position = rigidbody.transform.position;
        rigidBodyState.EulerAngles = rigidbody.transform.eulerAngles;
        rigidBodyState.Velocity = rigidbody.velocity;
        rigidBodyState.AngularVelocity = rigidbody.angularVelocity;
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
    public WeaponObjectState ToWeaponObjectState(uint id, WeaponComponent weaponComponent)
    {
        var state = new WeaponObjectState
        {
            Id = id,
            Type = weaponComponent.Type,
            BulletsLeftInMagazine = weaponComponent.BulletsLeftInMagazine,
            BulletsLeftOutOfMagazine = weaponComponent.BulletsLeftOutOfMagazine,
            RigidBodyState = ToRigidBodyState(weaponComponent.Rigidbody)
        };

        weaponComponent.Id = state.Id;

        return state;
    }
    public List<WeaponObjectState> ServerGetWeaponObjectStatesFromGameObjects(Server server)
    {
        var weaponObjectStates = new List<WeaponObjectState>();

        var weaponComponents = Object.FindObjectsOfType<WeaponComponent>();
        foreach (var weaponComponent in weaponComponents)
        {
            weaponObjectStates.Add(ToWeaponObjectState(server.GenerateNetworkId(), weaponComponent));
        }

        return weaponObjectStates;
    }
    public WeaponSpawnerState ToWeaponSpawnerState(uint id, WeaponSpawnerComponent weaponSpawnerComponent)
    {
        var state = new WeaponSpawnerState
        {
            Id = id,
            Type = weaponSpawnerComponent.WeaponType,
            TimeUntilNextSpawn = 0
        };

        return state;
    }
    public List<WeaponSpawnerState> ServerGetWeaponSpawnerStatesFromGameObjects(Server server)
    {
        var weaponSpawnerStates = new List<WeaponSpawnerState>();

        var weaponSpawnerComponents = Object.FindObjectsOfType<WeaponSpawnerComponent>();
        foreach (var weaponSpawnerComponent in weaponSpawnerComponents)
        {
            var weaponSpawnerState = ToWeaponSpawnerState(server.GenerateNetworkId(), weaponSpawnerComponent);
            weaponSpawnerStates.Add(weaponSpawnerState);

            weaponSpawnerComponent.Id = weaponSpawnerState.Id;
        }

        return weaponSpawnerStates;
    }
}