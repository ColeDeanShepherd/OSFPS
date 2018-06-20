using System;

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public class NonNullableAttribute : Attribute
{
}