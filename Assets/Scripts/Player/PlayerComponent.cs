using System.Collections.Generic;
using UnityEngine;

public class PlayerComponent : MonoBehaviour
{
    public static List<PlayerComponent> Instances = new List<PlayerComponent>();

    public PlayerState State;

    private void Awake()
    {
        Instances.Add(this);
    }
    private void OnDestroy()
    {
        Instances.Remove(this);
    }
}