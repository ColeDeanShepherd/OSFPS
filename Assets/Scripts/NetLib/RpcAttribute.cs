using System;

[AttributeUsage(AttributeTargets.Method)]
public class RpcAttribute : Attribute
{
    public NetworkPeerType ExecuteOn;
}