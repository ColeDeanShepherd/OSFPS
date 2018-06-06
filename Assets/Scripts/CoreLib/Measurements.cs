public static class Measurements
{
    public const double InchesInFoot = 12;
    public const double CentimetersInInch = 2.54;
    public const double CentimetersInMeter = 100;
    public const double FeetInMeter = CentimetersInMeter / CentimetersInInch / InchesInFoot;
    public const double InchesInMeter = CentimetersInMeter / CentimetersInInch;

    public const double HumanHeight = 1.9;
    public const double HumanHeadHeight = HumanHeight / 8;
    public const double HumanNeckLength = HumanHeadHeight / 3;
    public const double HumanTorsoLength = 2 * HumanHeadHeight;
    public const double HumanUpperArmLength = (5.0 / 3) * HumanHeadHeight;
    public const double HumanLowerArmLength = (5.0 / 4) * HumanHeadHeight;
    public const double HumanHandLength = (3.0 / 4) * HumanHeadHeight;
    public const double HumanUpperLegLength = (5.0 / 3) * HumanHeadHeight;
    public const double HumanUpperLegToHipsLength = (7.0 / 3) * HumanHeadHeight;
    public const double HumanLowerLegLength = (7.0 / 3) * HumanHeadHeight;
    public const double HumanFootLength = HumanHeadHeight;
}