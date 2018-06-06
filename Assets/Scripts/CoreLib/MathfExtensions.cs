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

    public static float LawOfCosinesSolveForAGivenThreeSideLengths(float a, float b, float c)
    {
        return Mathf.Acos((-(a * a) + (b * b) + (c * c)) / (2 * b * c));
    }
    public static float LawOfCosinesSolveForBGivenThreeSideLengths(float a, float b, float c)
    {
        return Mathf.Acos(((a * a) + -(b * b) + (c * c)) / (2 * a * c));
    }
    public static float LawOfCosinesSolveForCGivenThreeSideLengths(float a, float b, float c)
    {
        return Mathf.Acos(((a * a) + (b * b) + -(c * c)) / (2 * a * b));
    }


    public static void GetTwoBoneIkAngles(
        float bone1Length, float bone2Length, float targetDistance, bool getPositiveAngleSolution,
        out float theta1InRadians, out float theta2InRadians)
    {
        if ((bone1Length + bone2Length) < targetDistance)
        {
            theta1InRadians = 0;
            theta2InRadians = 0;
        }
        else if (Mathf.Abs(bone1Length - bone2Length) > targetDistance)
        {
            theta1InRadians = 0;
            theta2InRadians = Mathf.PI;
        }
        else
        {
            var a = bone2Length;
            var b = bone1Length;
            var c = targetDistance;
            var A = LawOfCosinesSolveForAGivenThreeSideLengths(a, b, c);
            var C = LawOfCosinesSolveForCGivenThreeSideLengths(a, b, c);

            if (getPositiveAngleSolution)
            {
                theta1InRadians = A;
                theta2InRadians = -(Mathf.PI - C);
            }
            else
            {
                theta1InRadians = -A;
                theta2InRadians = Mathf.PI - C;
            }
        }
    }
}