using System;

namespace NetworkLibrary
{
    [AttributeUsage(AttributeTargets.Method)]
    public class RpcAttribute : Attribute
    {
        public NetworkPeerType ExecuteOn;
    }
}