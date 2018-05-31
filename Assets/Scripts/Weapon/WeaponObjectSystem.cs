using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;

public class WeaponObjectSystem : ComponentSystem
{
    public struct Group
    {
        public WeaponComponent WeaponComponent;
    }

    public static WeaponObjectSystem Instance;

    public Dictionary<uint, Tuple<uint, float>> ClosestWeaponInfoByPlayerId;

    public WeaponObjectSystem()
    {
        Instance = this;
        ClosestWeaponInfoByPlayerId = new Dictionary<uint, Tuple<uint, float>>();
    }

    protected override void OnUpdate()
    {
        var deltaTime = Time.deltaTime;

        ClosestWeaponInfoByPlayerId.Clear();

        foreach (var weaponEntity in GetEntities<Group>())
        {
            UpdatePlayerClosestWeaponInfo(weaponEntity);
        }
    }

    private Collider[] colliderBuffer = new Collider[64];
    private void UpdatePlayerClosestWeaponInfo(Group weaponEntity)
    {
        var weaponComponent = weaponEntity.WeaponComponent;
        if (weaponComponent == null) return;

        var weaponPosition = weaponComponent.transform.position;
        var overlappingColliderCount = Physics.OverlapSphereNonAlloc(
            weaponPosition, OsFps.MaxWeaponPickUpDistance, colliderBuffer
        );

        for (var i = 0; i < overlappingColliderCount; i++)
        {
            var overlappingCollider = colliderBuffer[i];
            var playerObjectComponent = overlappingCollider.gameObject
                .FindComponentInObjectOrAncestor<PlayerObjectComponent>();

            if (playerObjectComponent != null)
            {
                var playerId = playerObjectComponent.State.Id;
                var distanceFromPlayerToWeapon = Vector3.Distance(
                    overlappingCollider.ClosestPoint(weaponPosition),
                    weaponPosition
                );
                var distanceFromPlayerToClosestWeapon = ClosestWeaponInfoByPlayerId
                    .GetValueOrDefault(playerId)
                    ?.Item2 ?? float.MaxValue;

                if (distanceFromPlayerToWeapon < distanceFromPlayerToClosestWeapon)
                {
                    var weaponId = weaponComponent.State.Id;
                    ClosestWeaponInfoByPlayerId[playerId] = new Tuple<uint, float>(
                        weaponId, distanceFromPlayerToClosestWeapon
                    );
                }
            }
        }
    }
}