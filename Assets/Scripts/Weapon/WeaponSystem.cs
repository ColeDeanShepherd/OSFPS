using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using System.Linq;

public class WeaponSystem : ComponentSystem
{
    public struct Data
    {
        public WeaponComponent WeaponComponent;
    }

    public static WeaponSystem Instance;

    public Dictionary<uint, System.Tuple<uint, float>> ClosestWeaponInfoByPlayerId;

    public WeaponSystem()
    {
        Instance = this;
        ClosestWeaponInfoByPlayerId = new Dictionary<uint, System.Tuple<uint, float>>();
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
    public WeaponDefinition GetWeaponDefinitionByType(WeaponType type)
    {
        return OsFps.Instance.WeaponDefinitionComponents
            .FirstOrDefault(wdc => wdc.Definition.Type == type)
            ?.Definition;
    }

    public void WeaponOnCollisionStay(WeaponComponent weaponComponent, Collision collision)
    {
        var otherGameObject = collision.gameObject;
        var playerObject = otherGameObject.FindObjectOrAncestorWithTag(OsFps.PlayerTag);

        if (playerObject != null)
        {
            PlayerObjectSystem.Instance.OnPlayerCollidingWithWeapon(playerObject, weaponComponent.gameObject);
        }
    }
    public void WeaponOnDestroy(WeaponComponent weaponComponent)
    {
        if (weaponComponent.State.WeaponSpawnerId.HasValue)
        {
            var weaponSpawnerComponent = WeaponSpawnerSystem.Instance.FindWeaponSpawnerComponent(
                weaponComponent.State.WeaponSpawnerId.Value
            );

            if (weaponSpawnerComponent != null)
            {
                weaponSpawnerComponent.State.TimeUntilNextSpawn = Instance.GetWeaponDefinitionByType(
                    weaponSpawnerComponent.State.Type
                ).SpawnInterval;
            }
        }
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
    public IEnumerable<Ray> ShotRays(WeaponDefinition weaponDefinition, Ray aimRay)
    {
        if (weaponDefinition.Type == WeaponType.Shotgun)
        {
            for (var i = 0; i < OsFps.ShotgunBulletsPerShot; i++)
            {
                var currentShotRay = MathfExtensions.GetRandomRayInCone(aimRay, weaponDefinition.ShotConeAngleInDegrees);
                yield return currentShotRay;
            }
        }
        else
        {
            var shotRay = MathfExtensions.GetRandomRayInCone(aimRay, weaponDefinition.ShotConeAngleInDegrees);
            yield return shotRay;
        }
    }
    public RaycastHit? GetClosestValidRaycastHitForGunShot(Ray shotRay, PlayerObjectComponent shootingPlayerObjectComponent)
    {
        var raycastHits = Physics.RaycastAll(shotRay);
        var closestValidRaycastHit = raycastHits
            .OrderBy(raycastHit => raycastHit.distance)
            .Select(raycastHit => (RaycastHit?)raycastHit)
            .FirstOrDefault(raycastHit =>
            {
                var hitPlayerObject = raycastHit.Value.collider.gameObject.FindObjectOrAncestorWithTag(OsFps.PlayerTag);
                return (hitPlayerObject == null) || (hitPlayerObject != shootingPlayerObjectComponent.gameObject);
            });
        return closestValidRaycastHit;
    }
    public GameObject CreateSniperBulletTrail(Ray ray)
    {
        var sniperBulletTrail = new GameObject("sniperBulletTrail");
        sniperBulletTrail.transform.position = ray.origin;
        sniperBulletTrail.transform.LookAt(ray.origin + ray.direction);

        var lineRenderer = sniperBulletTrail.AddComponent<LineRenderer>();
        lineRenderer.useWorldSpace = false;
        lineRenderer.SetPositions(new[]
        {
            Vector3.zero,
            2000 * Vector3.forward
        });
        lineRenderer.material = OsFps.Instance.SniperBulletTrailMaterial;

        var lineWidth = 0.1f;
        lineRenderer.startWidth = lineWidth;
        lineRenderer.endWidth = lineWidth;

        sniperBulletTrail.AddComponent<SniperRifleBulletTrailComponent>();

        Object.Destroy(sniperBulletTrail, OsFps.SniperRifleBulletTrailLifeTime);

        return sniperBulletTrail;
    }
    public void CreateHitScanShotDebugLine(Ray ray, Material material)
    {
        var hitScanShotObject = new GameObject("Hit Scan Shot");

        var lineRenderer = hitScanShotObject.AddComponent<LineRenderer>();
        var lineWidth = 0.05f;
        lineRenderer.startWidth = lineWidth;
        lineRenderer.endWidth = lineWidth;
        lineRenderer.SetPositions(new Vector3[] {
            ray.origin,
            ray.origin + (1000 * ray.direction)
        });
        lineRenderer.sharedMaterial = material;

        Object.Destroy(hitScanShotObject, OsFps.HitScanShotDebugLineLifetime);
    }

    public void ApplyExplosionDamageAndForces(
        Server server, Vector3 explosionPosition, float explosionRadius, float maxExplosionForce,
        float maxDamage, uint? attackerPlayerId
    )
    {
        // apply damage & forces to players within range
        var affectedColliders = Physics.OverlapSphere(explosionPosition, explosionRadius);
        var affectedColliderPlayerObjectComponents = affectedColliders
            .Select(collider => collider.gameObject.FindComponentInObjectOrAncestor<PlayerObjectComponent>())
            .ToArray();

        var affectedPlayerPointPairs = affectedColliders
            .Select((collider, colliderIndex) =>
                new System.Tuple<PlayerObjectComponent, Vector3>(
                    affectedColliderPlayerObjectComponents[colliderIndex],
                    collider.ClosestPoint(explosionPosition)
                )
            )
            .Where(pair => pair.Item1 != null)
            .GroupBy(pair => pair.Item1)
            .Select(g => g
                .OrderBy(pair => Vector3.Distance(pair.Item2, explosionPosition))
                .FirstOrDefault()
            )
            .ToArray();

        foreach (var pair in affectedPlayerPointPairs)
        {
            // Apply damage.
            var playerObjectComponent = pair.Item1;
            var closestPointToExplosion = pair.Item2;

            var distanceFromExplosion = Vector3.Distance(closestPointToExplosion, explosionPosition);
            var unclampedDamagePercent = (explosionRadius - distanceFromExplosion) / explosionRadius;
            var damagePercent = Mathf.Max(unclampedDamagePercent, 0);
            var damage = damagePercent * maxDamage;

            // TODO: don't call system directly
            var attackingPlayerObjectComponent = attackerPlayerId.HasValue
                ? PlayerObjectSystem.Instance.FindPlayerObjectComponent(attackerPlayerId.Value)
                : null;
            PlayerObjectSystem.Instance.ServerDamagePlayer(
                server, playerObjectComponent, damage, attackingPlayerObjectComponent
            );

            // Apply forces.
            var rigidbody = playerObjectComponent.gameObject.GetComponent<Rigidbody>();
            if (rigidbody != null)
            {
                rigidbody.AddExplosionForce(maxExplosionForce, explosionPosition, explosionRadius);
            }
        }

        for (var colliderIndex = 0; colliderIndex < affectedColliders.Length; colliderIndex++)
        {
            if (affectedColliderPlayerObjectComponents[colliderIndex] != null) continue;

            var collider = affectedColliders[colliderIndex];

            // Apply forces.
            var rigidbody = collider.gameObject.GetComponent<Rigidbody>();
            if (rigidbody != null)
            {
                rigidbody.AddExplosionForce(maxExplosionForce, explosionPosition, explosionRadius);
            }
        }
    }

    public void ShowMuzzleFlash(PlayerObjectComponent playerObjectComponent)
    {
        GameObject muzzleFlashObject = Object.Instantiate(
            OsFps.Instance.MuzzleFlashPrefab, Vector3.zero, Quaternion.identity
        );
        var barrelExitObject = playerObjectComponent.HandsPointObject.FindDescendant("BarrelExit");
        muzzleFlashObject.transform.SetParent(barrelExitObject.transform, false);

        Object.Destroy(muzzleFlashObject, OsFps.MuzzleFlashDuration);
    }
    public void ShowWeaponFireEffects(PlayerObjectComponent playerObjectComponent, Ray aimRay)
    {
        ShowMuzzleFlash(playerObjectComponent);

        var weapon = playerObjectComponent.State.CurrentWeapon;
        if (weapon != null)
        {
            if (weapon.Type == WeaponType.SniperRifle)
            {
                CreateSniperBulletTrail(aimRay);
            }

            var equippedWeaponComponent = PlayerObjectSystem.Instance.GetEquippedWeaponComponent(playerObjectComponent);
            if (equippedWeaponComponent != null)
            {
                var weaponAudioSource = equippedWeaponComponent.GetComponent<AudioSource>();
                weaponAudioSource?.PlayOneShot(weapon.Definition.ShotSound);

                equippedWeaponComponent.Animator.Play("Recoil");
            }

            if (weapon.Definition.IsHitScan)
            {
                foreach (var shotRay in WeaponSystem.Instance.ShotRays(weapon.Definition, aimRay))
                {
                    CreateBulletHole(playerObjectComponent, shotRay);
                }
            }
        }
    }
    public void CreateBulletHole(PlayerObjectComponent playerObjectComponent, Ray shotRay)
    {
        var possibleHit = WeaponSystem.Instance.GetClosestValidRaycastHitForGunShot(shotRay, playerObjectComponent);

        if (possibleHit != null)
        {
            var raycastHit = possibleHit.Value;
            var bulletHolePosition = raycastHit.point + (0.01f * raycastHit.normal);
            var bulletHoleOrientation = Quaternion.LookRotation(-raycastHit.normal);
            var bulletHole = Object.Instantiate(
                OsFps.Instance.BulletHolePrefab, bulletHolePosition, bulletHoleOrientation, raycastHit.transform
            );
            Object.Destroy(bulletHole, 5);
        }
    }
}