public class WeaponDefinition
{
    public WeaponType Type;
    public ushort MaxAmmo;
    public ushort BulletsPerMagazine;
    public float DamagePerBullet;
    public float HeadShotDamagePerBullet;
    public float ReloadTime;
    public float ShotInterval;
    public bool IsAutomatic;
    public float SpawnInterval;

    public ushort MaxAmmoOutOfMagazine
    {
        get
        {
            return (ushort)(MaxAmmo - BulletsPerMagazine);
        }
    }
}