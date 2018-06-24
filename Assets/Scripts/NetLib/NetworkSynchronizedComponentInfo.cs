using System;
using System.Collections.Generic;
using System.Reflection;

namespace NetworkLibrary
{
    public class NetworkSynchronizedComponentInfo
    {
        public Type StateType;
        public FieldInfo StateIdField;
        public List<NetworkSynchronizedFieldInfo> ThingsToSynchronize;

        public Type MonoBehaviourType;
        public FieldInfo MonoBehaviourStateField;
        public MethodInfo MonoBehaviourApplyStateMethod;
        public NetworkSynchronizedComponentAttribute SynchronizedComponentAttribute;
    }
    public class NetworkSynchronizedFieldInfo
    {
        public FieldInfo FieldInfo;
        public PropertyInfo PropertyInfo;
        public bool IsNullableIfReferenceType;
        public bool AreElementsNullableIfReferenceType;
    }
}