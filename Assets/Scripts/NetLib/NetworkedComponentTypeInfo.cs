using System;
using System.Collections.Generic;
using System.Reflection;

namespace NetworkLibrary
{
    public class NetworkedComponentTypeInfo
    {
        public Type StateType;
        public FieldInfo StateIdField;
        public List<NetworkedTypeFieldInfo> ThingsToSynchronize;

        public Type MonoBehaviourType;
        public FieldInfo MonoBehaviourStateField;
        public MethodInfo MonoBehaviourApplyStateMethod;
        public FieldInfo MonoBehaviourInstancesField;

        public NetworkedComponentAttribute SynchronizedComponentAttribute;
    }
    public class NetworkedTypeFieldInfo
    {
        public FieldInfo FieldInfo;
        public PropertyInfo PropertyInfo;
        public bool IsNullableIfReferenceType;
        public bool AreElementsNullableIfReferenceType;
    }
}