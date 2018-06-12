public static class BitUtilities
{
    public static bool GetBit(byte bits, byte bitIndex)
    {
        return ((bits >> bitIndex) & 1) == 1;
    }
}