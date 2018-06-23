using System;

namespace NetworkLibrary
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class NotNetworkSynchronizedAttribute : Attribute
    {
    }
}