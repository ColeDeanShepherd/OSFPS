using System;
using System.Collections.Generic;
using UnityEngine.Assertions;

public static class BitUtilities
{
    public static bool GetBit(byte bits, byte bitIndex)
    {
        Assert.IsTrue(bitIndex < 8);

        return ((bits >> bitIndex) & 1) == 1;
    }
    public static bool GetBit(ushort bits, byte bitIndex)
    {
        Assert.IsTrue(bitIndex < 16);

        return ((bits >> bitIndex) & 1) == 1;
    }
    public static bool GetBit(uint bits, byte bitIndex)
    {
        Assert.IsTrue(bitIndex < 32);

        return ((bits >> bitIndex) & 1) == 1;
    }
    public static bool GetBit(ulong bits, byte bitIndex)
    {
        Assert.IsTrue(bitIndex < 64);

        return ((bits >> bitIndex) & 1) == 1;
    }

    public static void ClearBit(ref byte bits, byte bitIndex)
    {
        Assert.IsTrue(bitIndex < 8);

        bits &= (byte)(~(1 << bitIndex));
    }
    public static void ClearBit(ref ushort bits, byte bitIndex)
    {
        Assert.IsTrue(bitIndex < 16);

        bits &= (ushort)(~(1 << bitIndex));
    }
    public static void ClearBit(ref uint bits, byte bitIndex)
    {
        Assert.IsTrue(bitIndex < 32);

        bits &= (uint)(~(1 << bitIndex));
    }
    public static void ClearBit(ref ulong bits, byte bitIndex)
    {
        Assert.IsTrue(bitIndex < 64);

        bits &= (ulong)(~(1 << bitIndex));
    }

    public static void SetBit(ref byte bits, byte bitIndex, bool value)
    {
        Assert.IsTrue(bitIndex < 8);

        ClearBit(ref bits, bitIndex);
        bits |= (byte)(Convert.ToUInt32(value) << bitIndex);
    }
    public static void SetBit(ref ushort bits, byte bitIndex, bool value)
    {
        Assert.IsTrue(bitIndex < 16);

        ClearBit(ref bits, bitIndex);
        bits |= (ushort)(Convert.ToUInt32(value) << bitIndex);
    }
    public static void SetBit(ref uint bits, byte bitIndex, bool value)
    {
        Assert.IsTrue(bitIndex < 32);

        ClearBit(ref bits, bitIndex);
        bits |= (Convert.ToUInt32(value) << bitIndex);
    }
    public static void SetBit(ref ulong bits, byte bitIndex, bool value)
    {
        Assert.IsTrue(bitIndex < 64);

        ClearBit(ref bits, bitIndex);
        bits |= (Convert.ToUInt64(value) << bitIndex);
    }

    public static IEnumerable<int> GetSetBitIndices(uint bits)
    {
        for (var i = 0; i < 32; i++)
        {
            if ((bits & 1) == 1)
            {
                yield return i;
            }

            bits >>= 1;
        }
    }
    public static IEnumerable<int> GetSetBitIndices(ulong bits)
    {
        for (var i = 0; i < 64; i++)
        {
            if ((bits & 1) == 1)
            {
                yield return i;
            }

            bits >>= 1;
        }
    }
}