using System;
using System.Reflection;

namespace NetworkLibrary
{
    public class NetworkSynchronizedComponentInfo
    {
        public Type StateType;
        public Type MonoBehaviourType;
        public FieldInfo MonoBehaviourStateField;
        public MethodInfo MonoBehaviourApplyStateMethod;
        public NetworkSynchronizedComponentAttribute SynchronizedComponentAttribute;
    }
}