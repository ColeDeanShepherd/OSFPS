using Unity.Mathematics;
using UnityEngine;

[System.Serializable]
public class RigidBodyState
{
    public static RigidBodyState FromRigidbody(Rigidbody rigidbody)
    {
        return new RigidBodyState
        {
            Position = rigidbody.transform.position,
            EulerAngles = rigidbody.transform.eulerAngles,
            Velocity = rigidbody.velocity,
            AngularVelocity = rigidbody.angularVelocity
        };
    }

    public float3 Position;
    public float3 EulerAngles;
    public float3 Velocity;
    public float3 AngularVelocity;
}