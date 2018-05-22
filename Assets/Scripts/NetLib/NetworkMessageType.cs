public enum NetworkMessageType : byte
{
    SetPlayerId = 100,
    GameState,
    SpawnPlayer,
    PlayerInput,
    TriggerPulled,
    ThrowGrenade,
    DetonateGrenade,
    Chat,
    ChangeWeapon
}