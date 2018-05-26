using UnityEngine;
using UnityEngine.Assertions;

public static class MathfExtensions
{
    public static float ToSignedAngleDegrees(float angle)
    {
        var positiveAngle = Mathf.Repeat(angle, 360);
        return (positiveAngle <= 180) ? positiveAngle : (positiveAngle - 360);
    }
    public static int Wrap(int value, int min, int max)
    {
        Assert.IsTrue(min <= max);

        var possibleValueCount = max - min + 1;

        while (value < min)
        {
            value += possibleValueCount;
        }

        while (value > max)
        {
            value -= possibleValueCount;
        }

        return value;
    }
}