using System.Linq;
using UnityEngine;
using Unity.Entities;

public class WeaponSystem : ComponentSystem
{
    public struct Group
    {
        public WeaponComponent WeaponComponent;
    }

    public static WeaponSystem Instance;

    public WeaponSystem()
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
    }

    private void ServerOnUpdate(Server server)
    {
        var deltaTime = Time.deltaTime;

        foreach (var entity in GetEntities<Group>())
        {
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
}