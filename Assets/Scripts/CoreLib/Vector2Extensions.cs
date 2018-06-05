using UnityEngine;

public static class Vector2Extensions
{
    public static Vector2 Rotate(Vector2 vector2, float angleInRadians)
    {
        var sinTheta = Mathf.Sin(angleInRadians);
        var cosTheta = Mathf.Cos(angleInRadians);

        return new Vector2(
            (vector2.x * cosTheta) + (vector2.y * sinTheta),
            -(vector2.x * sinTheta) + (vector2.y * cosTheta)
        );
    }
}