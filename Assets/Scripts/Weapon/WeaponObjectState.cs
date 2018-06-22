[System.Serializable]
[NetworkSynchronizedComponent(MonoBehaviourType = typeof(WeaponComponent))]
public class WeaponObjectState
{
    public uint Id;
    public WeaponType Type = WeaponType.Pistol;
    public ushort BulletsLeftInMagazine;
    public ushort BulletsLeftOutOfMagazine;
    public RigidBodyState RigidBodyState = new RigidBodyState();

    [NotNetworkSynchronized]
    public uint? WeaponSpawnerId;

    public ushort BulletsLeft
    {
        get
        {
            return (ushort)(BulletsLeftInMagazine + BulletsLeftOutOfMagazine);
        }
    }
    public WeaponDefinition Definition
    {
        get
        {
            return WeaponSystem.Instance.GetWeaponDefinitionByType(Type);
        }
    }
}