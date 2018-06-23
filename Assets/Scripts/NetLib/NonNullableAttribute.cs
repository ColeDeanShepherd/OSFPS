using System;

namespace NetworkLibrary
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class NonNullableAttribute : Attribute
    {
    }
}