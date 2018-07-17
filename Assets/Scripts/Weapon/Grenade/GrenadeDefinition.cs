using UnityEngine;

[System.Serializable]
public class GrenadeDefinition
{
    public GrenadeType Type;
    public float Damage;
    public float TimeAfterHitUntilDetonation;
    public float ExplosionRadius;
    public float SpawnInterval;
    public GameObject Prefab;
    public GameObject ExplosionPrefab;
    public Texture2D Icon;
}