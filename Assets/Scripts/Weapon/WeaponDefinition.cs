public class WeaponDefinition
{
    public WeaponType Type;
    public ushort MaxAmmo;
    public ushort BulletsPerMagazine;
    public int DamagePerBullet;
    public int HeadShotDamagePerBullet;
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