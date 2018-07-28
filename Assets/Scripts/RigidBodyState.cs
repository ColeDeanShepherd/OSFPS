using Unity.Mathematics;
using UnityEngine;

[System.Serializable]
public class RigidBodyState
{
    public const float VectorEqualityDistanceThreshold = 0.001f;

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

    public override bool Equals(object obj)
    {
        var other = obj as RigidBodyState;
        if (other == null) return false;

        return (
            (math.distance(Position, other.Position) <= VectorEqualityDistanceThreshold) &&
            (math.distance(EulerAngles, other.EulerAngles) <= VectorEqualityDistanceThreshold) &&
            (math.distance(Velocity, other.Velocity) <= VectorEqualityDistanceThreshold) &&
            (math.distance(AngularVelocity, other.AngularVelocity) <= VectorEqualityDistanceThreshold)
        );
    }
}