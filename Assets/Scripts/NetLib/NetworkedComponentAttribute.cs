using System;

namespace NetworkLibrary
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    public class NetworkedComponentAttribute : Attribute
    {
        public Type MonoBehaviourType;
    }
}