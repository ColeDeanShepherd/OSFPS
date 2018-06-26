using System.Collections.Generic;
using UnityEngine;

public class PlayerObjectComponent : MonoBehaviour
{
    public PlayerObjectState State;

    public List<PlayerLagCompensationSnapshot> LagCompensationSnapshots;

    public Rigidbody Rigidbody;
    public GameObject CameraPointObject;
    public GameObject HandsPointObject;
    
    private void Awake()
    {
        LagCompensationSnapshots = new List<PlayerLagCompensationSnapshot>();

        Rigidbody = GetComponent<Rigidbody>();
        CameraPointObject = transform.Find("CameraPoint").gameObject;
        HandsPointObject = gameObject.FindDescendant("HandsPoint");
    }
    private void LateUpdate()
    {
        State.Position = transform.position;
        State.Velocity = Rigidbody.velocity;
    }
    private void OnDestroy()
    {
        if (State.Id == OsFps.Instance.Client?.PlayerId)
        {
            OsFps.Instance.Client.DetachCameraFromPlayer();
        }
    }
    private void ApplyStateFromServer(object newState)
    {
        var updatedPlayerObjectState = (PlayerObjectState)newState;
        var client = OsFps.Instance.Client;
        var isPlayerMe = updatedPlayerObjectState.Id == client.PlayerId;
        var roundTripTime = client.ClientPeer.RoundTripTime.Value;
        var playerObjectComponent = this;

        // Update state.
        if (isPlayerMe)
        {
            if ((updatedPlayerObjectState.CurrentWeapon != null) && (State.CurrentWeapon != null))
            {
                updatedPlayerObjectState.CurrentWeapon.TimeSinceLastShot =
                    State.CurrentWeapon.TimeSinceLastShot;
            }

            updatedPlayerObjectState.ReloadTimeLeft = State.ReloadTimeLeft;
            updatedPlayerObjectState.TimeUntilCanThrowGrenade = State.TimeUntilCanThrowGrenade;
            updatedPlayerObjectState.Input = State.Input;
        }

        // Handle weapon pickup.
        var equippedWeaponType = client.GetEquippedWeaponComponent(playerObjectComponent)?.State.Type;
        var newWeaponType = updatedPlayerObjectState.CurrentWeapon?.Type;

        if (newWeaponType != equippedWeaponType)
        {
            client.VisualEquipWeapon(updatedPlayerObjectState);
        }

        // Update player object.
        // Correct position.
        var correctedPosition = OsFps.CorrectedPosition(
            updatedPlayerObjectState.Position, updatedPlayerObjectState.Velocity,
            roundTripTime, playerObjectComponent.transform.position
        );
        playerObjectComponent.transform.position = correctedPosition;
        updatedPlayerObjectState.Position = correctedPosition;

        // Correct velocity.
        var correctedVelocity = OsFps.CorrectedVelocity(
            updatedPlayerObjectState.Velocity, roundTripTime, playerObjectComponent.Rigidbody.velocity
        );
        playerObjectComponent.Rigidbody.velocity = correctedVelocity;
        updatedPlayerObjectState.Velocity = correctedVelocity;

        // Update look direction.
        if (isPlayerMe)
        {
            updatedPlayerObjectState.LookDirAngles = PlayerSystem.Instance.GetPlayerLookDirAngles(playerObjectComponent);
        }

        PlayerSystem.Instance.ApplyLookDirAnglesToPlayer(playerObjectComponent, updatedPlayerObjectState.LookDirAngles);

        // Update weapon if reloading.
        var equippedWeaponComponent = client.GetEquippedWeaponComponent(playerObjectComponent);
        if ((equippedWeaponComponent != null) && updatedPlayerObjectState.IsReloading)
        {
            var percentDoneReloading = updatedPlayerObjectState.ReloadTimeLeft / updatedPlayerObjectState.CurrentWeapon.Definition.ReloadTime;
            equippedWeaponComponent.Animator.SetFloat("Normalized Time", percentDoneReloading);
        }

        // Update weapon recoil.
        if ((equippedWeaponComponent != null) && (updatedPlayerObjectState.CurrentWeapon != null) && !updatedPlayerObjectState.IsReloading)
        {
            var percentDoneWithRecoil = Mathf.Min(
                updatedPlayerObjectState.CurrentWeapon.TimeSinceLastShot /
                updatedPlayerObjectState.CurrentWeapon.Definition.RecoilTime,
                1
            );
            equippedWeaponComponent.Animator.SetFloat("Normalized Time", percentDoneWithRecoil);
        }

        // Update shields.
        var shieldAlpha = 1.0f - (playerObjectComponent.State.Shield / OsFps.MaxPlayerShield);
        PlayerSystem.Instance.SetShieldAlpha(playerObjectComponent, shieldAlpha);
        
        State = updatedPlayerObjectState;
    }
}