using UnityEngine;

[System.Serializable]
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
    public bool IsHitScan;
    public float SpawnInterval;
    public float RecoilTime;
    public GameObject Prefab;
    public AudioClip ShotSound;
    public Texture2D Icon;

    public ushort MaxAmmoOutOfMagazine
    {
        get
        {
            return (ushort)(MaxAmmo - BulletsPerMagazine);
        }
    }
}