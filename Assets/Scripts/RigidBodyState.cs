using Unity.Mathematics;
using UnityEngine;

[System.Serializable]
public class RigidBodyState
{
    public float3 Position;
    public float3 EulerAngles;
    public float3 Velocity;
    public float3 AngularVelocity;
}