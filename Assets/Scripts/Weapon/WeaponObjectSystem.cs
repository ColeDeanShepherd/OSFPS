using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using System.Linq;

public class WeaponObjectSystem : ComponentSystem
{
    public struct Data
    {
        public WeaponComponent WeaponComponent;
    }

    public static WeaponObjectSystem Instance;

    public Dictionary<uint, System.Tuple<uint, float>> ClosestWeaponInfoByPlayerId;

    public WeaponObjectSystem()
    {
        Instance = this;
        ClosestWeaponInfoByPlayerId = new Dictionary<uint, System.Tuple<uint, float>>();
    }

    public int ServerAddBullets(EquippedWeaponState weaponState, int numBulletsToTryToAdd)
    {
        var numBulletsCanAdd = weaponState.Definition.MaxAmmo - weaponState.BulletsLeft;
        var bulletsToPickUp = Mathf.Min(numBulletsToTryToAdd, numBulletsCanAdd);
        var bulletsToAddInMagazine = Mathf.Min(bulletsToPickUp, weaponState.BulletsShotFromMagazine);

        weaponState.BulletsLeftInMagazine += (ushort)bulletsToAddInMagazine;
        weaponState.BulletsLeftOutOfMagazine += (ushort)(bulletsToPickUp - bulletsToAddInMagazine);

        return bulletsToPickUp;
    }
    public void ServerRemoveBullets(WeaponObjectState weaponObjectState, int numBulletsToRemove)
    {
        var bulletsToRemoveFromMagazine = Mathf.Min(weaponObjectState.BulletsLeftInMagazine, numBulletsToRemove);
        weaponObjectState.BulletsLeftInMagazine -= (ushort)bulletsToRemoveFromMagazine;
        numBulletsToRemove -= bulletsToRemoveFromMagazine;

        if (numBulletsToRemove > 0)
        {
            weaponObjectState.BulletsLeftOutOfMagazine -= (ushort)Mathf.Min(
                weaponObjectState.BulletsLeftOutOfMagazine,
                numBulletsToRemove
            );
        }
    }
    public WeaponComponent FindWeaponComponent(uint weaponId)
    {
        return Object.FindObjectsOfType<WeaponComponent>()
            .FirstOrDefault(wc => wc.State?.Id == weaponId);
    }
    public GameObject FindWeaponObject(uint weaponId)
    {
        var weaponComponent = FindWeaponComponent(weaponId);
        return weaponComponent?.gameObject;
    }
    public GameObject CreateSniperBulletTrail(Ray ray)
    {
        var sniperBulletTrail = new GameObject("sniperBulletTrail");
        var lineRenderer = sniperBulletTrail.AddComponent<LineRenderer>();
        lineRenderer.SetPositions(new[]
        {
            ray.origin,
            ray.origin + (2000 * ray.direction)
        });
        lineRenderer.material = OsFps.Instance.SniperBulletTrailMaterial;

        var lineWidth = 0.1f;
        lineRenderer.startWidth = lineWidth;
        lineRenderer.endWidth = lineWidth;

        Object.Destroy(sniperBulletTrail, 1);

        return sniperBulletTrail;
    }
    public WeaponDefinition GetWeaponDefinitionByType(WeaponType type)
    {
        return OsFps.Instance.WeaponDefinitionComponents
            .FirstOrDefault(wdc => wdc.Definition.Type == type)
            ?.Definition;
    }
    public IEnumerable<Ray> ShotRays(WeaponDefinition weaponDefinition, Ray aimRay)
    {
        if (weaponDefinition.Type == WeaponType.Shotgun)
        {
            for (var i = 0; i < OsFps.ShotgunBulletsPerShot; i++)
            {
                var currentShotRay = MathfExtensions.GetRandomRayInCone(aimRay, OsFps.ShotgunShotConeAngleInDegrees);
                yield return currentShotRay;
            }
        }
        else if (weaponDefinition.Type == WeaponType.Smg)
        {
            var shotRay = MathfExtensions.GetRandomRayInCone(aimRay, OsFps.SmgShotConeAngleInDegrees);
            yield return shotRay;
        }
        else if (weaponDefinition.Type == WeaponType.AssaultRifle)
        {
            var shotRay = MathfExtensions.GetRandomRayInCone(aimRay, OsFps.AssaultRifleShotConeAngleInDegrees);
            yield return shotRay;
        }
        else
        {
            yield return aimRay;
        }
    }

    protected override void OnUpdate()
    {
        var deltaTime = Time.deltaTime;

        ClosestWeaponInfoByPlayerId.Clear();

        foreach (var entity in GetEntities<Data>())
        {
            UpdatePlayerClosestWeaponInfo(entity.WeaponComponent);
        }
    }

    private Collider[] colliderBuffer = new Collider[64];
    private void UpdatePlayerClosestWeaponInfo(WeaponComponent weaponComponent)
    {
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
                    ClosestWeaponInfoByPlayerId[playerId] = new System.Tuple<uint, float>(
                        weaponId, distanceFromPlayerToClosestWeapon
                    );
                }
            }
        }
    }
}