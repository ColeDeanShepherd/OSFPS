using System;

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public class NotNetworkSynchronizedAttribute : Attribute
{
}