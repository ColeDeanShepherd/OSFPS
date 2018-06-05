using UnityEngine;

public static class Vector3Extensions
{
    public static Vector3 Reject(Vector3 a, Vector3 b)
    {
        return a - Vector3.Project(a, b);
    }
    public static float ScalarProject(Vector3 a, Vector3 b)
    {
        return Vector3.Dot(a, b) / b.magnitude;
    }
}