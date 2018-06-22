using System;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public class NetworkSynchronizedComponentAttribute : Attribute
{
    public Type MonoBehaviourType;
}