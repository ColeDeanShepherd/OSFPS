using UnityEngine;

public static class MathfExtensions
{
    public static float ToSignedAngleDegrees(float angle)
    {
        var positiveAngle = Mathf.Repeat(angle, 360);
        return (positiveAngle <= 180) ? positiveAngle : (positiveAngle - 360);
    }
}