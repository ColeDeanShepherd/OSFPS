using System.Linq;
using UnityEngine;
using Unity.Entities;
using System.Collections.Generic;
using UnityEngine.Assertions;
using Unity.Mathematics;

public class PlayerObjectSystem : ComponentSystem
{
    public struct Data
    {
        public PlayerObjectComponent PlayerObjectComponent;
    }

    public static PlayerObjectSystem Instance;

    public PlayerObjectSystem()
    {
        Instance = this;
    }

    protected override void OnUpdate()
    {
        var server = OsFps.Instance?.Server;
        if (server != null)
        {
            ServerOnUpdate(server);
        }

        var client = OsFps.Instance?.Client;
        if (client != null)
        {
            ClientOnUpdate(client);
        }
    }
    public void OnLateUpdate()
    {
        var server = OsFps.Instance?.Server;
        if (server != null)
        {
            ServerOnLateUpdate(server);
        }

        var client = OsFps.Instance?.Client;
        if (client != null)
        {
            ClientOnLateUpdate(client);
        }
    }

    private void ServerOnUpdate(Server server)
    {
        foreach (var entity in GetEntities<Data>())
        {
            var playerObjectState = entity.PlayerObjectComponent.State;
            var wasReloadingBeforeUpdate = playerObjectState.IsReloading;

            UpdatePlayer(entity.PlayerObjectComponent);

            if (wasReloadingBeforeUpdate && (playerObjectState.ReloadTimeLeft <= 0))
            {
                ServerPlayerFinishReload(entity.PlayerObjectComponent);
            }

            DrawPlayerInput(entity.PlayerObjectComponent);
        }
    }
    public void ServerOnLateUpdate(Server server)
    {
        UpdateLagCompensationSnapshots();
    }
    public void ClientOnLateUpdate(Client client)
    {
        UpdateLagCompensationSnapshots();
    }
    private void UpdateLagCompensationSnapshots()
    {
        foreach (var entity in GetEntities<Data>())
        {
            var playerObjectComponent = entity.PlayerObjectComponent;
            var currentTime = Time.realtimeSinceStartup;

            // Remove old snapshots.
            playerObjectComponent.LagCompensationSnapshots = playerObjectComponent.LagCompensationSnapshots
                .Where(snapshot => (currentTime - snapshot.Time) <= OsFps.LagCompensationBufferTime)
                .ToList();

            playerObjectComponent.LagCompensationSnapshots.Add(
                GetLagCompensationSnapshot(playerObjectComponent, currentTime)
            );
        }
    }
    private void DrawLagCompensationSnapshots()
    {
        foreach (var entity in GetEntities<Data>())
        {
            foreach (var snapshot in entity.PlayerObjectComponent.LagCompensationSnapshots)
            {
                var rayOrigin = snapshot.Position + new float3(0, 1, 0);
                var rayDirection = Quaternion.Euler(snapshot.LookDirAngles.x, snapshot.LookDirAngles.y, 0) * Vector3.forward;
                Debug.DrawRay(rayOrigin, rayDirection);
            }
        }
    }
    private PlayerLagCompensationSnapshot GetLagCompensationSnapshot(PlayerObjectComponent playerObjectComponent, float currentTime)
    {
        return new PlayerLagCompensationSnapshot
        {
            Time = currentTime,
            Position = playerObjectComponent.transform.position,
            LookDirAngles = GetPlayerLookDirAngles(playerObjectComponent)
        };
    }

    private void DrawPlayerInput(PlayerObjectComponent playerObjectComponent)
    {
        var relativeMoveDirection = GetRelativeMoveDirection(playerObjectComponent.State.Input);
        var playerYAngle = playerObjectComponent.transform.eulerAngles.y;
        var horizontalMoveDirection = Quaternion.Euler(new Vector3(0, playerYAngle, 0)) * relativeMoveDirection;
        var moveRay = new Ray(playerObjectComponent.transform.position + Vector3.up, horizontalMoveDirection);
        Debug.DrawLine(moveRay.origin, moveRay.origin + moveRay.direction);
    }

    public void ServerDamagePlayer(Server server, PlayerObjectComponent playerObjectComponent, float damage, PlayerObjectComponent attackingPlayerObjectComponent)
    {
        if (!playerObjectComponent.State.IsAlive || (damage <= 0)) return;

        var playerObjectState = playerObjectComponent.State;

        var damageToShield = Mathf.Min(damage, playerObjectState.Shield);
        playerObjectState.Shield -= damageToShield;

        var damageToHealth = damage - damageToShield;
        playerObjectState.Health -= damageToHealth;

        playerObjectState.TimeUntilShieldCanRegen = OsFps.TimeAfterDamageUntilShieldRegen;

        var playerComponent = FindPlayerComponent(playerObjectState.Id);
        var playerState = playerComponent.State;

        if (!playerObjectState.IsAlive)
        {
            for (var i = 0; i < playerObjectComponent.State.Weapons.Length; i++)
            {
                ServerPlayerDropWeapon(server, playerObjectComponent, i);
            }

            for (var i = 0; i < playerObjectComponent.State.GrenadeSlots.Length; i++)
            {
                ServerPlayerDropGrenades(server, playerObjectComponent, i);
            }

            // The player object will be destroyed later by the RemoveDeadPlayerSystem.
            playerState.RespawnTimeLeft = OsFps.RespawnTime;

            // Update scores
            playerState.Deaths++;

            if (attackingPlayerObjectComponent != null)
            {
                var attackingPlayerId = attackingPlayerObjectComponent.State.Id;
                var attackingPlayerComponent = FindPlayerComponent(attackingPlayerId);
                attackingPlayerComponent.State.Kills++;
            }

            // Send message.
            server.ServerPeer.CallRpcOnAllClients("ClientOnReceiveChatMessage", server.reliableSequencedChannelId, new
            {
                playerId = (uint?)null,
                message = GetKillMessage(playerObjectComponent, attackingPlayerObjectComponent)
            });
        }
    }

    public void ServerPlayerDropWeapon(Server server, PlayerObjectComponent playerObjectComponent, int weaponIndex)
    {
        var playerWeapons = playerObjectComponent.State.Weapons;
        var equippedWeaponState = playerWeapons[weaponIndex];
        if ((equippedWeaponState == null)) return;

        var weaponObjectState = new WeaponObjectState
        {
            Id = server.GenerateNetworkId(),
            Type = equippedWeaponState.Type,
            BulletsLeftInMagazine = equippedWeaponState.BulletsLeftInMagazine,
            BulletsLeftOutOfMagazine = equippedWeaponState.BulletsLeftOutOfMagazine,
            RigidBodyState = new RigidBodyState
            {
                Position = playerObjectComponent.HandsPointObject.transform.position,
                EulerAngles = Vector3.zero,
                Velocity = Vector3.zero,
                AngularVelocity = Vector3.zero
            }
        };
        WeaponSpawnerSystem.Instance.SpawnLocalWeaponObject(weaponObjectState);

        playerObjectComponent.State.Weapons[weaponIndex] = null;
    }
    public void ServerPlayerDropGrenades(Server server, PlayerObjectComponent playerObjectComponent, int grenadeSlotIndex)
    {
        var playerGrenadeSlots = playerObjectComponent.State.GrenadeSlots;
        var grenadeSlot = playerGrenadeSlots[grenadeSlotIndex];
        if ((grenadeSlot == null) || (grenadeSlot.GrenadeCount == 0)) return;

        for (var i = 0; i < grenadeSlot.GrenadeCount; i++)
        {
            var grenadeState = new GrenadeState
            {
                Id = server.GenerateNetworkId(),
                Type = grenadeSlot.GrenadeType,
                RigidBodyState = new RigidBodyState
                {
                    Position = playerObjectComponent.HandsPointObject.transform.position,
                    EulerAngles = Vector3.zero,
                    Velocity = Vector3.zero,
                    AngularVelocity = Vector3.zero
                },
                IsActive = false,
                TimeUntilDetonation = null
            };

            GrenadeSpawnerSystem.Instance.SpawnLocalGrenadeObject(grenadeState);
        }

        playerGrenadeSlots[grenadeSlotIndex] = null;
    }
    
    public Ray GetShotRay(PlayerObjectComponent playerObjectComponent)
    {
        return new Ray(
            playerObjectComponent.CameraPointObject.transform.position,
            playerObjectComponent.CameraPointObject.transform.forward
        );
    }
    public void ServerShoot(
        Server server, PlayerObjectComponent shootingPlayerObjectComponent, Ray shotRay, float secondsToRewind
    )
    {
        var shootingPlayerObjectState = shootingPlayerObjectComponent.State;
        if (!shootingPlayerObjectState.CanShoot) return;

        var playerObjectComponent = FindPlayerObjectComponent(shootingPlayerObjectState.Id);
        if (playerObjectComponent == null) return;

        var weaponState = shootingPlayerObjectState.CurrentWeapon;

        var weaponDefinition = weaponState.Definition;
        if (weaponState.Definition.IsHitScan)
        {
            ServerFireHitscanWeapon(server, shootingPlayerObjectComponent, weaponDefinition, shotRay, secondsToRewind);
        }
        else
        {
            if (weaponDefinition.Type == WeaponType.RocketLauncher)
            {
                ServerFireRocketLauncher(server, shootingPlayerObjectComponent, shotRay);
            }
        }

        weaponState.BulletsLeftInMagazine--;
        weaponState.TimeSinceLastShot = 0;
    }
    public PlayerLagCompensationSnapshot InterpolateLagCompensationSnapshots(
        PlayerLagCompensationSnapshot snapshot1, PlayerLagCompensationSnapshot snapshot2, float rewoundTime
    )
    {
        var interpolationPercent = (rewoundTime - snapshot1.Time) / (snapshot2.Time - snapshot1.Time);

        return new PlayerLagCompensationSnapshot
        {
            Time = Mathf.Lerp(snapshot1.Time, snapshot2.Time, interpolationPercent),
            Position = Vector3.Lerp(snapshot1.Position, snapshot2.Position, interpolationPercent),
            LookDirAngles = Vector2.Lerp(
                snapshot1.LookDirAngles, snapshot2.LookDirAngles, interpolationPercent
            )
        };
    }
    public void ApplyLagCompensationSnapshot(
        PlayerObjectComponent playerObjectComponent, PlayerLagCompensationSnapshot snapshot
    )
    {
        playerObjectComponent.transform.position = snapshot.Position;
        ApplyLookDirAnglesToPlayer(playerObjectComponent, snapshot.LookDirAngles);
        
        if (OsFps.ShowLagCompensationOnServer)
        {
            var tmpCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tmpCube.transform.position = snapshot.Position + (2 * new float3(0, 1, 0));
            tmpCube.transform.localScale = 0.25f * Vector3.one;
            tmpCube.transform.eulerAngles = new Vector3(snapshot.LookDirAngles.x, snapshot.LookDirAngles.y);

            Object.DestroyImmediate(tmpCube.GetComponent<BoxCollider>());
            Object.Destroy(tmpCube, 2);
        }
    }
    public PlayerLagCompensationSnapshot GetInterpolatedLagCompensationSnapshot(
        PlayerObjectComponent playerObjectComponent, float rewoundTime
    )
    {
        var snapshots = playerObjectComponent.LagCompensationSnapshots;
        var indexOfFirstSnapshotBeforeRewoundTime = snapshots.FindLastIndex(s => s.Time < rewoundTime);
        var indexOfFirstSnapshotAfterRewoundTime = snapshots.FindIndex(s => s.Time >= rewoundTime);

        if ((indexOfFirstSnapshotBeforeRewoundTime < 0) && (indexOfFirstSnapshotAfterRewoundTime < 0))
        {
            var playerId = playerObjectComponent.State.Id;
            OsFps.Logger.LogWarning($"Could not find any snapshot to rewind to for player {playerId}. This is expected when joining a server.");
            return GetLagCompensationSnapshot(playerObjectComponent, Time.realtimeSinceStartup);
        }

        PlayerLagCompensationSnapshot rewoundSnapshot;
        if (indexOfFirstSnapshotBeforeRewoundTime < 0)
        {
            rewoundSnapshot = snapshots[indexOfFirstSnapshotAfterRewoundTime];
        }
        else if (indexOfFirstSnapshotAfterRewoundTime < 0)
        {
            rewoundSnapshot = snapshots[indexOfFirstSnapshotBeforeRewoundTime];
        }
        else
        {
            rewoundSnapshot = InterpolateLagCompensationSnapshots(
                snapshots[indexOfFirstSnapshotBeforeRewoundTime],
                snapshots[indexOfFirstSnapshotAfterRewoundTime],
                rewoundTime
            );
        }

        return rewoundSnapshot;
    }
    public void ServerRewindPlayers(float secondsToRewind)
    {
        var curTime = Time.realtimeSinceStartup;
        var rewoundTime = curTime - secondsToRewind;
        foreach (var entity in GetEntities<Data>())
        {
            // Add a current snapshot for un-rewinding later.
            entity.PlayerObjectComponent.LagCompensationSnapshots.Add(
                GetInterpolatedLagCompensationSnapshot(entity.PlayerObjectComponent, curTime)
            );

            var rewoundSnapshot = GetInterpolatedLagCompensationSnapshot(entity.PlayerObjectComponent, rewoundTime);
            ApplyLagCompensationSnapshot(entity.PlayerObjectComponent, rewoundSnapshot);
        }
    }
    public void ServerUnRewindPlayers()
    {
        foreach (var entity in GetEntities<Data>())
        {
            if (entity.PlayerObjectComponent == null)
            {
                continue;
            }

            var snapshots = entity.PlayerObjectComponent.LagCompensationSnapshots;
            var currentSnapshot = snapshots[snapshots.Count - 1];
            ApplyLagCompensationSnapshot(entity.PlayerObjectComponent, currentSnapshot);
        }
    }
    public void ServerFireHitscanWeapon(
        Server server, PlayerObjectComponent shootingPlayerObjectComponent,
        WeaponDefinition weaponDefinition, Ray aimRay, float secondsToRewind
    )
    {
        var shootingPlayerObjectState = shootingPlayerObjectComponent.State;

        ServerRewindPlayers(secondsToRewind);

        foreach (var shotRay in WeaponObjectSystem.Instance.ShotRays(weaponDefinition, aimRay))
        {
            ServerApplyHitscanShot(server, shootingPlayerObjectComponent, weaponDefinition, shotRay);
        }

        ServerUnRewindPlayers();
    }
    public void ServerApplyHitscanShot(
        Server server, PlayerObjectComponent shootingPlayerObjectComponent,
        WeaponDefinition weaponDefinition, Ray shotRay
    )
    {
        var raycastHits = Physics.RaycastAll(shotRay);

        foreach (var hit in raycastHits)
        {
            var hitPlayerObject = hit.collider.gameObject.FindObjectOrAncestorWithTag(OsFps.PlayerTag);

            if ((hitPlayerObject != null) && (hitPlayerObject != shootingPlayerObjectComponent.gameObject))
            {
                var hitPlayerObjectComponent = hitPlayerObject.GetComponent<PlayerObjectComponent>();

                var isHeadShot = hit.collider.gameObject.name == OsFps.PlayerHeadColliderName;
                var damage = !isHeadShot
                    ? weaponDefinition.DamagePerBullet
                    : weaponDefinition.HeadShotDamagePerBullet;
                ServerDamagePlayer(
                    server, hitPlayerObjectComponent, damage, shootingPlayerObjectComponent
                );
            }

            if (hit.rigidbody != null)
            {
                hit.rigidbody.AddForceAtPosition(5 * shotRay.direction, hit.point, ForceMode.Impulse);
            }
        }

        if (OsFps.ShowHitScanShotsOnServer)
        {
            OsFps.Instance.CreateHitScanShotDebugLine(shotRay, OsFps.Instance.ServerShotRayMaterial);
        }
    }
    public void ServerFireRocketLauncher(
        Server server, PlayerObjectComponent shootingPlayerObjectComponent, Ray shotRay
    )
    {
        var rocketState = new RocketState
        {
            Id = server.GenerateNetworkId(),
            RigidBodyState = new RigidBodyState
            {
                Position = shotRay.origin + shotRay.direction,
                EulerAngles = Quaternion.LookRotation(shotRay.direction, Vector3.up).eulerAngles,
                Velocity = OsFps.RocketSpeed * shotRay.direction,
                AngularVelocity = Vector3.zero
            },
            ShooterPlayerId = shootingPlayerObjectComponent.State.Id
        };
        var rocket = RocketSystem.Instance.SpawnLocalRocketObject(rocketState);

        // Make grenade ignore collisions with thrower.
        GameObjectExtensions.IgnoreCollisionsRecursive(rocket, shootingPlayerObjectComponent.gameObject);
    }
    public void ServerPlayerStartReload(PlayerObjectComponent playerObjectComponent)
    {
        var playerObjectState = playerObjectComponent.State;
        if (!playerObjectState.IsAlive) return;

        var weapon = playerObjectState.CurrentWeapon;
        if (weapon == null) return;

        playerObjectState.ReloadTimeLeft = weapon.Definition.ReloadTime;
    }
    public void ServerPlayerFinishReload(PlayerObjectComponent playerObjectComponent)
    {
        var weapon = playerObjectComponent.State.CurrentWeapon;
        if (weapon == null) return;

        var bulletsUsedInMagazine = weapon.Definition.BulletsPerMagazine - weapon.BulletsLeftInMagazine;
        var bulletsToAddToMagazine = (ushort)Mathf.Min(bulletsUsedInMagazine, weapon.BulletsLeftOutOfMagazine);

        weapon.BulletsLeftInMagazine += bulletsToAddToMagazine;
        weapon.BulletsLeftOutOfMagazine -= bulletsToAddToMagazine;
    }

    public void ServerOnPlayerCollidingWithWeapon(Server server, GameObject playerObject, GameObject weaponObject)
    {
        /*var playerObjectComponent = playerObject.GetComponent<PlayerObjectComponent>();
        var weaponComponent = weaponObject.GetComponent<WeaponComponent>();

        ServerPlayerTryToPickupWeapon(playerObjectComponent, weaponComponent);*/
    }
    
    private EquippedWeaponState ToEquippedWeaponState(WeaponObjectState weaponObjectState)
    {
        return new EquippedWeaponState
        {
            Type = weaponObjectState.Type,
            BulletsLeftInMagazine = weaponObjectState.BulletsLeftInMagazine,
            BulletsLeftOutOfMagazine = weaponObjectState.BulletsLeftOutOfMagazine,
            TimeSinceLastShot = weaponObjectState.Definition.ShotInterval
        };
    }
    public void ServerPlayerTryToPickupWeapon(
        Server server, PlayerObjectComponent playerObjectComponent, WeaponComponent weaponComponent
    )
    {
        var playerState = playerObjectComponent.State;
        if (!playerState.IsAlive) return;

        var weaponObjectState = weaponComponent.State;
        var playersMatchingWeapon = playerState.Weapons.FirstOrDefault(
            w => (w != null) && (w.Type == weaponComponent.State.Type)
        );

        if (playerState.HasEmptyWeapon)
        {
            var emptyWeaponIndex = System.Array.FindIndex(playerState.Weapons, w => w == null);
            playerState.Weapons[emptyWeaponIndex] = ToEquippedWeaponState(weaponObjectState);
            playerState.CurrentWeaponIndex = (byte)emptyWeaponIndex;

            Object.Destroy(weaponComponent.gameObject);
        }
        else if (playersMatchingWeapon != null)
        {
            var numBulletsPickedUp = WeaponObjectSystem.Instance.ServerAddBullets(
                playersMatchingWeapon, weaponObjectState.BulletsLeft
            );
            WeaponObjectSystem.Instance.ServerRemoveBullets(weaponObjectState, numBulletsPickedUp);

            if (weaponObjectState.BulletsLeft == 0)
            {
                Object.Destroy(weaponComponent.gameObject);
            }
        }
        else
        {
            // drop current weapon
            ServerPlayerDropWeapon(server, playerObjectComponent, playerState.CurrentWeaponIndex);

            // pick up other weapon
            playerState.Weapons[playerState.CurrentWeaponIndex] = ToEquippedWeaponState(weaponObjectState);
            Object.Destroy(weaponComponent.gameObject);
        }
    }
    public void ServerOnPlayerCollidingWithGrenade(GameObject playerObject, GameObject grenadeObject)
    {
        var playerObjectComponent = playerObject.GetComponent<PlayerObjectComponent>();
        var playerState = playerObjectComponent.State;

        if (!playerState.IsAlive) return;

        var grenadeComponent = grenadeObject.GetComponent<GrenadeComponent>();
        var grenadeState = grenadeComponent.State;

        if (grenadeState.IsActive) return;

        // Try to find a grenade slot with a matching grenade type.
        var grenadeSlot = playerState.GrenadeSlots.FirstOrDefault(gs =>
            (gs != null) &&
            (gs.GrenadeType == grenadeState.Type)
        );

        // Try to find a grenade slot with a different type but 0 grenades.
        if (grenadeSlot == null)
        {
            grenadeSlot = playerState.GrenadeSlots.FirstOrDefault(gs =>
                (gs != null) &&
                (gs.GrenadeCount == 0)
            );
            grenadeSlot.GrenadeType = grenadeState.Type;
        }

        // Try to find a null grenade slot.
        if (grenadeSlot == null)
        {
            var nullSlotIndex = System.Array.FindIndex(playerState.GrenadeSlots, gs => gs == null);
            if (nullSlotIndex >= 0)
            {
                grenadeSlot = new GrenadeSlot
                {
                    GrenadeType = grenadeState.Type,
                    GrenadeCount = 0
                };
                playerState.GrenadeSlots[nullSlotIndex] = grenadeSlot;
            }
        }

        if ((grenadeSlot == null) || (grenadeSlot.GrenadeCount >= OsFps.MaxGrenadesPerType)) return;

        grenadeSlot.GrenadeCount++;
        Object.Destroy(grenadeObject);
    }

    public void UpdatePlayerMovement(PlayerObjectState playerObjectState)
    {
        var playerObjectComponent = FindPlayerObjectComponent(playerObjectState.Id);
        if (playerObjectComponent == null) return;

        ApplyLookDirAnglesToPlayer(playerObjectComponent, playerObjectState.LookDirAngles);

        var isGrounded = IsPlayerGrounded(playerObjectComponent);

        if (isGrounded)
        {
            var relativeMoveDirection = GetRelativeMoveDirection(playerObjectState.Input);
            var playerYAngle = playerObjectComponent.transform.eulerAngles.y;
            var horizontalMoveDirection = Quaternion.Euler(new Vector3(0, playerYAngle, 0)) * relativeMoveDirection;
            var desiredHorizontalVelocity = OsFps.MaxPlayerMovementSpeed * horizontalMoveDirection;
            var currentHorizontalVelocity = GameObjectExtensions.GetHorizontalVelocity(playerObjectComponent.Rigidbody);
            var horizontalVelocityError = desiredHorizontalVelocity - currentHorizontalVelocity;

            playerObjectComponent.Rigidbody.AddForce(3000 * horizontalVelocityError);
        }
    }
    public void Jump(PlayerObjectComponent playerObjectComponent)
    {
        Assert.IsNotNull(playerObjectComponent);

        var playerVelocity = playerObjectComponent.Rigidbody.velocity;
        var newPlayerVelocity = new Vector3(playerVelocity.x, OsFps.PlayerInitialJumpSpeed, playerVelocity.z);
        playerObjectComponent.Rigidbody.velocity = newPlayerVelocity;
    }

    public void SetShieldAlpha(PlayerObjectComponent playerObjectComponent, float alpha)
    {
        foreach (var meshRenderer in playerObjectComponent.GetComponentsInChildren<MeshRenderer>())
        {
            var shieldDownMaterial = meshRenderer.materials
                .FirstOrDefault(m => m.name.Contains(OsFps.Instance.ShieldDownMaterial.name));
            if (shieldDownMaterial != null)
            {
                shieldDownMaterial.SetFloat(OsFps.ShieldDownMaterialAlphaParameterName, alpha);
            }
        }
    }

    private string GetKillMessage(PlayerObjectComponent killedPlayerObjectComponent, PlayerObjectComponent attackerPlayerObjectComponent)
    {
        var killedPlayerComponent = FindPlayerComponent(killedPlayerObjectComponent.State.Id);
        var attackerPlayerComponent = (attackerPlayerObjectComponent != null)
            ? FindPlayerComponent(attackerPlayerObjectComponent.State.Id)
            : null;

        return (attackerPlayerObjectComponent != null)
            ? string.Format("{0} killed {1}.", attackerPlayerComponent.State.Name, killedPlayerComponent.State.Name)
            : string.Format("{0} died.", killedPlayerComponent.State.Name);
    }

    private void ClientOnUpdate(Client client)
    {
        foreach (var entity in GetEntities<Data>())
        {
            var playerObjectComponent = entity.PlayerObjectComponent;
            var playerObjectState = playerObjectComponent.State;

            if (playerObjectState.Id == client.PlayerId)
            {
                if (!client._isShowingChatMessageInput && !client._isShowingMenu)
                {
                    ClientUpdateThisPlayer(client, playerObjectComponent);
                }
                else
                {
                    playerObjectState.Input = new PlayerInput();
                }
            }

            UpdatePlayer(playerObjectComponent);
            DrawPlayerInput(playerObjectComponent);
        }
    }
    private void ClientUpdateThisPlayer(Client client, PlayerObjectComponent playerObjectComponent)
    {
        var playerObjectState = playerObjectComponent.State;
        playerObjectState.Input = GetCurrentPlayersInput();

        var unscaledDeltaMouse = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
        var deltaMouse = client.GetMouseSensitivityForZoomLevel() * unscaledDeltaMouse;

        playerObjectState.LookDirAngles = new Vector2(
            Mathf.Clamp(MathfExtensions.ToSignedAngleDegrees(playerObjectState.LookDirAngles.x - deltaMouse.y), -90, 90),
            Mathf.Repeat(playerObjectState.LookDirAngles.y + deltaMouse.x, 360)
        );

        if (Input.GetButtonDown("Reload") && playerObjectState.CanReload)
        {
            client.Reload(playerObjectComponent);
        }

        if (playerObjectState.Input.IsFirePressed)
        {
            var wasTriggerJustPulled = Input.GetButtonDown("Fire");

            if (
                playerObjectState.CanShoot &&
                (wasTriggerJustPulled || playerObjectState.CurrentWeapon.Definition.IsAutomatic)
            )
            {
                client.PlayerShoot(playerObjectComponent);
            }
        }

        if (Input.GetButtonDown("Throw Grenade") && playerObjectState.CanThrowGrenade)
        {
            client.ThrowGrenade(playerObjectState);
        }

        if (Input.GetButtonDown("Switch Grenade"))
        {
            client.SwitchGrenadeType(playerObjectState);
        }
    }

    public PlayerInput GetCurrentPlayersInput()
    {
        return new PlayerInput
        {
            IsMoveFowardPressed = Input.GetButton("Move Forward"),
            IsMoveBackwardPressed = Input.GetButton("Move Backward"),
            IsMoveRightPressed = Input.GetButton("Move Right"),
            IsMoveLeftPressed = Input.GetButton("Move Left"),
            IsFirePressed = Input.GetButton("Fire")
        };
    }
    public Vector3 GetRelativeMoveDirection(PlayerInput input)
    {
        var moveDirection = Vector3.zero;

        if (input.IsMoveFowardPressed)
        {
            moveDirection += Vector3.forward;
        }

        if (input.IsMoveBackwardPressed)
        {
            moveDirection += Vector3.back;
        }

        if (input.IsMoveRightPressed)
        {
            moveDirection += Vector3.right;
        }

        if (input.IsMoveLeftPressed)
        {
            moveDirection += Vector3.left;
        }

        return moveDirection.normalized;
    }

    public bool IsPlayerGrounded(PlayerObjectComponent playerObjectComponent)
    {
        var sphereRadius = 0.4f;
        var spherePosition = playerObjectComponent.transform.position + new Vector3(0, 0.3f, 0);

        var intersectingColliders = Physics.OverlapSphere(spherePosition, sphereRadius);
        return intersectingColliders.Any(collider =>
        {
            var otherPlayerObjectComponent = collider.gameObject.FindComponentInObjectOrAncestor<PlayerObjectComponent>();
            return (
                (otherPlayerObjectComponent == null) ||
                (otherPlayerObjectComponent.State.Id != playerObjectComponent.State.Id)
            );
        });
    }

    public void UpdatePlayer(PlayerObjectComponent playerObjectComponent)
    {
        var playerObjectState = playerObjectComponent.State;

        var client = OsFps.Instance?.Client;
        var equippedWeaponComponent = client?.GetEquippedWeaponComponent(playerObjectComponent);

        // reload
        if (playerObjectState.IsReloading)
        {
            playerObjectState.ReloadTimeLeft -= Time.deltaTime;

            if (equippedWeaponComponent != null)
            {
                var percentDoneReloading = playerObjectState.ReloadTimeLeft / playerObjectState.CurrentWeapon.Definition.ReloadTime;
                equippedWeaponComponent.Animator.SetFloat("Normalized Time", percentDoneReloading);
            }
        }

        // shot interval
        if (playerObjectState.CurrentWeapon != null)
        {
            playerObjectState.CurrentWeapon.TimeSinceLastShot += Time.deltaTime;

            if ((equippedWeaponComponent != null) && !playerObjectState.IsReloading)
            {
                var percentDoneWithRecoil = Mathf.Min(
                    playerObjectState.CurrentWeapon.TimeSinceLastShot /
                    playerObjectState.CurrentWeapon.Definition.RecoilTime,
                    1
                );
                equippedWeaponComponent.Animator.SetFloat("Normalized Time", percentDoneWithRecoil);
            }
        }

        // grenade throw interval
        if (playerObjectState.TimeUntilCanThrowGrenade > 0)
        {
            playerObjectState.TimeUntilCanThrowGrenade -= Time.deltaTime;
        }

        // shield regen interval
        float shieldRegenTime;
        if (playerObjectState.TimeUntilShieldCanRegen > 0)
        {
            playerObjectState.TimeUntilShieldCanRegen -= Time.deltaTime;
            shieldRegenTime = (playerObjectState.TimeUntilShieldCanRegen <= 0)
                ? Mathf.Abs(playerObjectState.TimeUntilShieldCanRegen)
                : 0;
        }
        else
        {
            shieldRegenTime = Time.deltaTime;
        }

        var shieldRegenAmount = shieldRegenTime * OsFps.ShieldRegenPerSecond;
        playerObjectState.Shield = Mathf.Min(playerObjectState.Shield + shieldRegenAmount, OsFps.MaxPlayerShield);

        // update movement
        UpdatePlayerMovement(playerObjectState);
    }

    public Vector2 GetPlayerLookDirAngles(PlayerObjectComponent playerObjectComponent)
    {
        return new Vector2(
            playerObjectComponent.CameraPointObject.transform.localEulerAngles.x,
            playerObjectComponent.transform.eulerAngles.y
        );
    }
    public void ApplyLookDirAnglesToPlayer(PlayerObjectComponent playerObjectComponent, Vector2 LookDirAngles)
    {
        playerObjectComponent.transform.localEulerAngles = new Vector3(0, LookDirAngles.y, 0);
        playerObjectComponent.CameraPointObject.transform.localEulerAngles = new Vector3(LookDirAngles.x, 0, 0);
    }

    // probably too much boilerplate here
    public void OnPlayerCollidingWithWeapon(GameObject playerObject, GameObject weaponObject)
    {
        if (OsFps.Instance.Server != null)
        {
            ServerOnPlayerCollidingWithWeapon(OsFps.Instance.Server, playerObject, weaponObject);
        }
    }
    public void OnPlayerCollidingWithGrenade(GameObject playerObject, GameObject grenadeObject)
    {
        if (OsFps.Instance.Server != null)
        {
            ServerOnPlayerCollidingWithGrenade(playerObject, grenadeObject);
        }
    }

    public GameObject FindPlayerObject(uint playerId)
    {
        var playerObjectComponent = FindPlayerObjectComponent(playerId);
        return playerObjectComponent?.gameObject;
    }
    public PlayerComponent FindPlayerComponent(uint playerId)
    {
        return Object.FindObjectsOfType<PlayerComponent>()
            .FirstOrDefault(pc => pc.State.Id == playerId);
    }
    public PlayerObjectComponent FindPlayerObjectComponent(uint playerId)
    {
        return Object.FindObjectsOfType<PlayerObjectComponent>()
            .FirstOrDefault(poc => poc.State.Id == playerId);
    }
    public GameObject CreateLocalPlayerDataObject(PlayerState playerState)
    {
        var playerDataObject = new GameObject($"Player {playerState.Id}");

        var playerComponent = playerDataObject.AddComponent<PlayerComponent>();
        playerComponent.State = playerState;

        playerDataObject.AddComponent<GameObjectEntity>();

        return playerDataObject;
    }
}