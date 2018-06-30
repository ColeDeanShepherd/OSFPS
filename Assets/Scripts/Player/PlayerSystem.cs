using Unity.Entities;
using System.Collections.Generic;

public class PlayerSystem : ComponentSystem
{
    public struct Data
    {
        public PlayerComponent PlayerComponent;
    }

    public static PlayerSystem Instance;

    public PlayerSystem()
    {
        Instance = this;
    }

    protected override void OnUpdate()
    {
    }
}