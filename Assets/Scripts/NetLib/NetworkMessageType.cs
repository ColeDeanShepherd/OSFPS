public enum NetworkMessageType : byte
{
    SetPlayerId,
    GameState,
    SpawnPlayer,
    PlayerInput,
    TriggerPulled,
    ReloadPressed,
    ThrowGrenade,
    DetonateGrenade,
    Chat,
    ChangeWeapon
}