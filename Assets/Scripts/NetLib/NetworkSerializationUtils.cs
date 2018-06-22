using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Assertions;

public static class NetworkSerializationUtils
{
    public static byte[] SerializeRpcCall(RpcInfo rpcInfo, object argumentsObj)
    {
        var argumentsType = argumentsObj.GetType();
        var argumentProperties = argumentsType.GetProperties();

        Assert.IsTrue(argumentProperties.Length == rpcInfo.ParameterTypes.Length);

        using (var memoryStream = new MemoryStream())
        {
            using (var binaryWriter = new BinaryWriter(memoryStream))
            {
                binaryWriter.Write(rpcInfo.Id);

                for (var i = 0; i < rpcInfo.ParameterTypes.Length; i++)
                {
                    var parameterName = rpcInfo.ParameterNames[i];
                    var parameterType = rpcInfo.ParameterTypes[i];

                    var argumentProperty = argumentProperties.First(argField =>
                        argField.Name == parameterName
                    );
                    var argumentType = argumentProperty.PropertyType;
                    var argument = argumentProperty.GetValue(argumentsObj);

                    Assert.IsTrue(
                        argumentType.IsEquivalentTo(parameterType),
                        $"RPC parameter {parameterName} has type {parameterType.AssemblyQualifiedName} but was passed {argumentType.AssemblyQualifiedName}."
                    );

                    SerializeObject(
                        binaryWriter, argument, argumentType, isNullableIfReferenceType: false,
                        areElementsNullableIfReferenceType: false
                    );
                }
            }

            return memoryStream.ToArray();
        }
    }
    public static object[] DeserializeRpcCallArguments(RpcInfo rpcInfo, BinaryReader reader)
    {
        return rpcInfo.ParameterTypes
            .Select(parameterType =>
                Deserialize(
                    reader, parameterType, isNullableIfReferenceType: false,
                    areElementsNullableIfReferenceType: false
                )
            )
            .ToArray();
    }

    public static Type GetSmallestUIntTypeWithNumberOfBits(uint numberOfBits)
    {
        if (numberOfBits <= 8)
        {
            return typeof(byte);
        }
        else if (numberOfBits <= 16)
        {
            return typeof(ushort);
        }
        else if (numberOfBits <= 32)
        {
            return typeof(uint);
        }
        else if (numberOfBits <= 64)
        {
            return typeof(ulong);
        }
        else
        {
            throw new NotImplementedException();
        }
    }
    public static Type GetSmallestUIntTypeForMaxValue(ulong maxValue)
    {
        if (maxValue <= byte.MaxValue)
        {
            return typeof(byte);
        }
        else if (maxValue <= ushort.MaxValue)
        {
            return typeof(ushort);
        }
        else if (maxValue <= uint.MaxValue)
        {
            return typeof(uint);
        }
        else if (maxValue <= ulong.MaxValue)
        {
            return typeof(ulong);
        }
        else
        {
            throw new NotImplementedException();
        }
    }
    public static Type GetSmallestUIntTypeToHoldEnumValues(Type enumType)
    {
        Assert.IsTrue(enumType.IsEnum);

        var numEnumValues = enumType.GetEnumValues().Length;
        return GetSmallestUIntTypeForMaxValue((ulong)numEnumValues);
    }

    public static void Serialize<T>(
        BinaryWriter writer, T t, bool isNullableIfReferenceType, bool areElementsNullableIfReferenceType
    )
    {
        SerializeObject(writer, t, typeof(T), isNullableIfReferenceType, areElementsNullableIfReferenceType);
    }
    public static void SerializeObject(
        BinaryWriter writer, object obj, Type overrideType, bool isNullableIfReferenceType,
        bool areElementsNullableIfReferenceType
    )
    {
        Assert.IsTrue(!isNullableIfReferenceType || (overrideType != null));

        var objType = overrideType ?? obj?.GetType();
        Assert.IsNotNull(objType);
        Assert.IsTrue((obj != null) || !objType.IsClass || isNullableIfReferenceType);

        var nullableUnderlyingType = Nullable.GetUnderlyingType(objType);
        if ((nullableUnderlyingType == null) && isNullableIfReferenceType && objType.IsClass)
        {
            nullableUnderlyingType = objType;
        }

        if (nullableUnderlyingType != null)
        {
            writer.Write(obj != null);

            if (obj != null)
            {
                objType = nullableUnderlyingType;
            }
            else
            {
                return;
            }
        }

        if (objType == typeof(bool))
        {
            writer.Write((bool)obj);
        }
        else if (objType == typeof(sbyte))
        {
            writer.Write((sbyte)obj);
        }
        else if (objType == typeof(byte))
        {
            writer.Write((byte)obj);
        }
        else if (objType == typeof(ushort))
        {
            writer.Write((ushort)obj);
        }
        else if (objType == typeof(short))
        {
            writer.Write((short)obj);
        }
        else if (objType == typeof(uint))
        {
            writer.Write((uint)obj);
        }
        else if (objType == typeof(int))
        {
            writer.Write((int)obj);
        }
        else if (objType == typeof(ulong))
        {
            writer.Write((ulong)obj);
        }
        else if (objType == typeof(long))
        {
            writer.Write((long)obj);
        }
        else if (objType == typeof(float))
        {
            writer.Write((float)obj);
        }
        else if (objType == typeof(double))
        {
            writer.Write((double)obj);
        }
        else if (objType == typeof(decimal))
        {
            writer.Write((decimal)obj);
        }
        else if (objType == typeof(char))
        {
            writer.Write((char)obj);
        }
        else if (objType == typeof(string))
        {
            writer.Write((string)obj);
        }
        else if (objType == typeof(Vector2))
        {
            Serialize(writer, (Vector2)obj);
        }
        else if (objType == typeof(Vector3))
        {
            Serialize(writer, (Vector3)obj);
        }
        else if (typeof(INetworkSerializable).IsAssignableFrom(obj.GetType()))
        {
            ((INetworkSerializable)obj).Serialize(writer);
        }
        else if (typeof(ICollection).IsAssignableFrom(objType))
        {
            Type elementType;

            if (objType.IsArray)
            {
                elementType = objType.GetElementType();
            }
            else if (objType.IsGenericType)
            {
                elementType = objType.GetGenericArguments()[0];
            }
            else
            {
                throw new NotImplementedException("Non-generic collections aren't supported.");
            }

            var collection = (ICollection)obj;
            writer.Write((uint)collection.Count);

            foreach (var element in collection)
            {
                SerializeObject(
                    writer, element, overrideType: elementType,
                    isNullableIfReferenceType: areElementsNullableIfReferenceType,
                    areElementsNullableIfReferenceType: false
                );
            }
        }
        else
        {
            if (objType.IsEnum)
            {
                var smallestTypeToHoldEnumValues = GetSmallestUIntTypeToHoldEnumValues(objType);
                var objToSerialize = Convert.ChangeType(obj, smallestTypeToHoldEnumValues);
                SerializeObject(
                    writer, objToSerialize, overrideType: null, isNullableIfReferenceType: false,
                    areElementsNullableIfReferenceType: false
                );
            }
            else if (objType.IsClass || objType.IsValueType)
            {
                var objFields = objType.GetFields();
                foreach (var objField in objFields)
                {
                    SerializeObject(
                        writer, objField.GetValue(obj), objField.FieldType, isNullableIfReferenceType: false,
                        areElementsNullableIfReferenceType: false
                    );
                }

                var objProperties = objType.GetProperties();
                foreach (var objProperty in objProperties)
                {
                    SerializeObject(
                        writer, objProperty.GetValue(obj), objProperty.PropertyType,
                        isNullableIfReferenceType: false, areElementsNullableIfReferenceType: false
                    );
                }
            }
            else
            {
                throw new NotImplementedException($"Cannot serialize type: {objType.AssemblyQualifiedName}");
            }
        }
    }

    public static uint GetChangeMask(Type type, object oldValue, object newValue)
    {
        var typeInfo = GetTypeToNetworkSynchronizeInfo(type);
        Assert.IsTrue(typeInfo.NumberOfThingsToSynchronize <= (8 * sizeof(uint)));

        uint changeMask = 0;
        uint changeMaskBitIndex = 0;

        foreach (var field in typeInfo.FieldsToSynchronize)
        {
            var oldFieldValue = field.GetValue(oldValue);
            var newFieldValue = field.GetValue(newValue);

            BitUtilities.SetBit(ref changeMask, (byte)changeMaskBitIndex, !object.Equals(newFieldValue, oldFieldValue));
            changeMaskBitIndex++;
        }

        foreach (var property in typeInfo.PropertiesToSynchronize)
        {
            var oldPropertyValue = property.GetValue(oldValue);
            var newPropertyValue = property.GetValue(newValue);

            BitUtilities.SetBit(ref changeMask, (byte)changeMaskBitIndex, !object.Equals(newPropertyValue, oldPropertyValue));
            changeMaskBitIndex++;
        }

        return changeMask;
    }

    public struct TypeToNetworkSynchronizeInfo
    {
        public FieldInfo[] FieldsToSynchronize;
        public PropertyInfo[] PropertiesToSynchronize;
        
        public int NumberOfThingsToSynchronize
        {
            get
            {
                return FieldsToSynchronize.Length + PropertiesToSynchronize.Length;
            }
        }
    }
    private static TypeToNetworkSynchronizeInfo GetTypeToNetworkSynchronizeInfo(Type type)
    {
        return new TypeToNetworkSynchronizeInfo
        {
            FieldsToSynchronize = type.GetFields()
                .Where(field => !Attribute.IsDefined(field, typeof(NotNetworkSynchronizedAttribute)))
                .ToArray(),
            PropertiesToSynchronize = type.GetProperties()
                .Where(property =>
                    property.CanRead &&
                    property.CanWrite &&
                    !Attribute.IsDefined(property, typeof(NotNetworkSynchronizedAttribute))
                )
                .ToArray()
        };
    }

    public static void SerializeGivenChangeMask(BinaryWriter writer, Type type, object value, uint changeMask)
    {
        var typeInfo = GetTypeToNetworkSynchronizeInfo(type);
        uint changeMaskBitIndex = 0;

        foreach (var field in typeInfo.FieldsToSynchronize)
        {
            if (BitUtilities.GetBit(changeMask, (byte)changeMaskBitIndex))
            {
                var isNullableIfReferenceType = field.FieldType.IsClass && !Attribute.IsDefined(field, typeof(NonNullableAttribute));
                var areElementsNullableIfReferenceType = !Attribute.IsDefined(field, typeof(NonNullableElementAttribute));
                SerializeObject(
                    writer, field.GetValue(value), field.FieldType, isNullableIfReferenceType, areElementsNullableIfReferenceType
                );
            }
            changeMaskBitIndex++;
        }

        foreach (var property in typeInfo.PropertiesToSynchronize)
        {
            if (BitUtilities.GetBit(changeMask, (byte)changeMaskBitIndex))
            {
                var isNullableIfReferenceType = property.PropertyType.IsClass && !Attribute.IsDefined(property, typeof(NonNullableAttribute));
                var areElementsNullableIfReferenceType = !Attribute.IsDefined(property, typeof(NonNullableElementAttribute));
                SerializeObject(
                    writer, property.GetValue(value), property.PropertyType, isNullableIfReferenceType, areElementsNullableIfReferenceType
                );
            }
            changeMaskBitIndex++;
        }
    }
    public static void SerializeDelta(BinaryWriter writer, Type type, object oldValue, object newValue)
    {
        var changeMask = GetChangeMask(type, oldValue, newValue);
        writer.Write(changeMask);
        SerializeGivenChangeMask(writer, type, newValue, changeMask);
    }

    public static void DeserializeGivenChangeMask(BinaryReader reader, Type type, object oldValue, uint changeMask)
    {
        var typeInfo = GetTypeToNetworkSynchronizeInfo(type);
        byte changeMaskBitIndex = 0;

        foreach (var field in typeInfo.FieldsToSynchronize)
        {
            if (BitUtilities.GetBit(changeMask, changeMaskBitIndex))
            {
                var isNullableIfReferenceType = field.FieldType.IsClass && !Attribute.IsDefined(field, typeof(NonNullableAttribute));
                var areElementsNullableIfReferenceType = !Attribute.IsDefined(field, typeof(NonNullableElementAttribute));
                var newFieldValue = Deserialize(
                    reader, field.FieldType, isNullableIfReferenceType, areElementsNullableIfReferenceType
                );
                field.SetValue(oldValue, newFieldValue);
            }
            changeMaskBitIndex++;
        }

        foreach (var property in typeInfo.PropertiesToSynchronize)
        {
            if (BitUtilities.GetBit(changeMask, changeMaskBitIndex))
            {
                var isNullableIfReferenceType = property.PropertyType.IsClass && !Attribute.IsDefined(property, typeof(NonNullableAttribute));
                var areElementsNullableIfReferenceType = !Attribute.IsDefined(property, typeof(NonNullableElementAttribute));
                var newPropertyValue = Deserialize(
                    reader, property.PropertyType, isNullableIfReferenceType, areElementsNullableIfReferenceType
                );
                property.SetValue(oldValue, newPropertyValue);
            }
            changeMaskBitIndex++;
        }
    }
    public static void DeserializeDelta(BinaryReader reader, Type type, object oldValue)
    {
        var changeMask = reader.ReadUInt32();
        DeserializeGivenChangeMask(reader, type, oldValue, changeMask);
    }

    public static object Deserialize(
        BinaryReader reader, Type type,
        bool isNullableIfReferenceType, bool areElementsNullableIfReferenceType
    )
    {
        var nullableUnderlyingType = Nullable.GetUnderlyingType(type);
        if ((nullableUnderlyingType == null) && isNullableIfReferenceType && type.IsClass)
        {
            nullableUnderlyingType = type;
        }

        if (nullableUnderlyingType != null)
        {
            var objHasValue = reader.ReadBoolean();
            return objHasValue
                ? Deserialize(
                    reader, nullableUnderlyingType, isNullableIfReferenceType: false,
                    areElementsNullableIfReferenceType: areElementsNullableIfReferenceType
                ) : null;
        }
        else if (type == typeof(bool))
        {
            return reader.ReadBoolean();
        }
        else if (type == typeof(sbyte))
        {
            return reader.ReadSByte();
        }
        else if (type == typeof(byte))
        {
            return reader.ReadByte();
        }
        else if (type == typeof(ushort))
        {
            return reader.ReadUInt16();
        }
        else if (type == typeof(short))
        {
            return reader.ReadInt16();
        }
        else if (type == typeof(uint))
        {
            return reader.ReadUInt32();
        }
        else if (type == typeof(int))
        {
            return reader.ReadInt32();
        }
        else if (type == typeof(ulong))
        {
            return reader.ReadUInt64();
        }
        else if (type == typeof(long))
        {
            return reader.ReadInt64();
        }
        else if (type == typeof(float))
        {
            return reader.ReadSingle();
        }
        else if (type == typeof(double))
        {
            return reader.ReadDouble();
        }
        else if (type == typeof(decimal))
        {
            return reader.ReadDecimal();
        }
        else if (type == typeof(char))
        {
            return reader.ReadChar();
        }
        else if (type == typeof(string))
        {
            return reader.ReadString();
        }
        else if (type == typeof(Vector2))
        {
            var result = new Vector2();
            Deserialize(reader, ref result);

            return result;
        }
        else if (type == typeof(Vector3))
        {
            var result = new Vector3();
            Deserialize(reader, ref result);

            return result;
        }
        else if (typeof(INetworkSerializable).IsAssignableFrom(type))
        {
            var result = Activator.CreateInstance(type);
            ((INetworkSerializable)result).Deserialize(reader);

            return result;
        }
        else if (typeof(IEnumerable).IsAssignableFrom(type))
        {
            if (typeof(Array).IsAssignableFrom(type))
            {
                var elementType = type.GetElementType();
                var elementCount = reader.ReadUInt32();
                var array = Array.CreateInstance(elementType, elementCount);

                for (var i = 0; i < elementCount; i++)
                {
                    var element = Deserialize(
                        reader, elementType, isNullableIfReferenceType: areElementsNullableIfReferenceType,
                        areElementsNullableIfReferenceType: false
                    );
                    array.SetValue(element, i);
                }

                return array;
            }
            else if (typeof(IList).IsAssignableFrom(type))
            {
                var list = (IList)Activator.CreateInstance(type);
                var elementType = type.GenericTypeArguments[0];
                var elementCount = reader.ReadUInt32();

                for (var i = 0; i < elementCount; i++)
                {
                    var element = Deserialize(
                        reader, elementType, isNullableIfReferenceType: areElementsNullableIfReferenceType,
                        areElementsNullableIfReferenceType: false
                    );
                    list.Add(element);
                }

                return list;
            }
            else
            {
                throw new NotImplementedException();
            }
        }
        else
        {
            if (type.IsEnum)
            {
                var smallestTypeToHoldEnumValues = GetSmallestUIntTypeToHoldEnumValues(type);
                var enumValueAsInt = Deserialize(
                    reader, smallestTypeToHoldEnumValues, isNullableIfReferenceType: false,
                    areElementsNullableIfReferenceType: false
                );

                return Enum.ToObject(type, enumValueAsInt);
            }
            else if (type.IsClass || type.IsValueType)
            {
                var result = Activator.CreateInstance(type);

                var objFields = type.GetFields();
                foreach (var field in objFields)
                {
                    var fieldValue = Deserialize(
                        reader, field.FieldType, isNullableIfReferenceType: false,
                        areElementsNullableIfReferenceType: false
                    );
                    field.SetValue(result, fieldValue);
                }

                var objProperties = type.GetProperties();
                foreach (var property in objProperties)
                {
                    var propertyValue = Deserialize(
                        reader, property.PropertyType, isNullableIfReferenceType: false,
                        areElementsNullableIfReferenceType: false
                    );
                    property.SetValue(result, propertyValue);
                }

                return result;
            }
            else
            {
                throw new NotImplementedException($"Cannot deserialize type: {type.AssemblyQualifiedName}");
            }
        }
    }
    public static T Deserialize<T>(BinaryReader reader, bool isNullableIfReferenceType, bool areElementsNullableIfReferenceType)
    {
        return (T)Deserialize(reader, typeof(T), isNullableIfReferenceType, areElementsNullableIfReferenceType);
    }

    public static void Serialize(BinaryWriter writer, Vector2 v)
    {
        writer.Write(v.x);
        writer.Write(v.y);
    }
    public static void Deserialize(BinaryReader reader, ref Vector2 v)
    {
        v.x = reader.ReadSingle();
        v.y = reader.ReadSingle();
    }
    public static void Serialize(BinaryWriter writer, Vector3 v)
    {
        writer.Write(v.x);
        writer.Write(v.y);
        writer.Write(v.z);
    }
    public static void Deserialize(BinaryReader reader, ref Vector3 v)
    {
        v.x = reader.ReadSingle();
        v.y = reader.ReadSingle();
        v.z = reader.ReadSingle();
    }

    public static void Serialize<T>(BinaryWriter writer, List<T> list) where T : INetworkSerializable
    {
        writer.Write(list.Count);

        foreach(var element in list)
        {
            element.Serialize(writer);
        }
    }
    public static void Deserialize<T>(BinaryReader reader, List<T> list) where T : INetworkSerializable, new()
    {
        list.Clear();

        var listSize = reader.ReadInt32();

        for(var i = 0; i < listSize; i++)
        {
            var element = new T();
            element.Deserialize(reader);

            list.Add(element);
        }
    }

    public static void Serialize<T>(BinaryWriter writer, List<T> list, Action<BinaryWriter, T> serializeElementFunc)
    {
        writer.Write(list.Count);

        foreach (var element in list)
        {
            serializeElementFunc(writer, element);
        }
    }
    public static void Deserialize<T>(BinaryReader reader, List<T> list, Func<BinaryReader, T> deserializeElementFunc)
    {
        list.Clear();

        var listSize = reader.ReadInt32();

        for (var i = 0; i < listSize; i++)
        {
            var element = deserializeElementFunc(reader);
            list.Add(element);
        }
    }

    public static void SerializeNullable<T>(BinaryWriter writer, T value) where T : INetworkSerializable
    {
        writer.Write(value != null);

        if (value != null)
        {
            value.Serialize(writer);
        }
    }
    public static T DeserializeNullable<T>(BinaryReader reader) where T : INetworkSerializable, new()
    {
        var isNotNull = reader.ReadBoolean();

        if (isNotNull)
        {
            var value = new T();
            value.Deserialize(reader);

            return value;
        }
        else
        {
            return default(T);
        }
    }


    public class NetworkSynchronizedComponentInfo
    {
        public System.Type StateType;
        public System.Type MonoBehaviourType;
        public System.Reflection.FieldInfo MonoBehaviourStateField;
        public NetworkSynchronizedComponentAttribute SynchronizedComponentAttribute;
    }

    public static List<NetworkSynchronizedComponentInfo> GetNetworkSynchronizedComponentInfos()
    {
        var assembly = System.Reflection.Assembly.GetExecutingAssembly();

        return assembly.GetTypes()
            .Select(type =>
            {
                var synchronizedComponentAttribute = (NetworkSynchronizedComponentAttribute)type.GetCustomAttributes(
                    typeof(NetworkSynchronizedComponentAttribute), inherit: false
                ).FirstOrDefault();

                if (synchronizedComponentAttribute == null)
                {
                    return null;
                }

                var monoBehaviourType = synchronizedComponentAttribute.MonoBehaviourType;
                var stateField = monoBehaviourType.GetFields()
                    .FirstOrDefault(f => f.FieldType.IsEquivalentTo(type));

                return new NetworkSynchronizedComponentInfo
                {
                    StateType = type,
                    MonoBehaviourType = monoBehaviourType,
                    MonoBehaviourStateField = stateField,
                    SynchronizedComponentAttribute = synchronizedComponentAttribute
                };
            })
            .Where(t => t != null)
            .ToList();
    }
    public static List<object> GetStateObjectsToSynchronize(NetworkSynchronizedComponentInfo synchronizedComponentInfo)
    {
        var monoBehaviours = UnityEngine.Object.FindObjectsOfType(synchronizedComponentInfo.MonoBehaviourType);

        return monoBehaviours
            .Select(mb => synchronizedComponentInfo.MonoBehaviourStateField.GetValue(mb))
            .ToList();
    }
    public static List<List<object>> GetStateObjectListsToSynchronize(List<NetworkSynchronizedComponentInfo> synchronizedComponentInfos)
    {
        return synchronizedComponentInfos
            .Select(GetStateObjectsToSynchronize)
            .ToList();
    }
    public static void SerializeSynchronizedComponents(
        BinaryWriter writer, List<NetworkSynchronizedComponentInfo> synchronizedComponentInfos
    )
    {
        foreach (var stateObjects in GetStateObjectListsToSynchronize(synchronizedComponentInfos))
        {
            Serialize(writer, stateObjects, (binaryWriter, stateObject) =>
            {
                var changeMask = uint.MaxValue;

                binaryWriter.Write(changeMask);
                SerializeGivenChangeMask(
                    binaryWriter, stateObject.GetType(), stateObject, changeMask
                );
            });
        }
    }
    public static List<List<object>> DeserializeSynchronizedComponents(
        BinaryReader reader, List<NetworkSynchronizedComponentInfo> synchronizedComponentInfos
    )
    {
        var stateObjectLists = new List<List<object>>();

        foreach (var synchronizedComponentInfo in synchronizedComponentInfos)
        {
            var stateObjects = new List<object>();

            Deserialize(reader, stateObjects, binaryReader =>
            {
                var stateObject = System.Activator.CreateInstance(synchronizedComponentInfo.StateType);
                DeserializeDelta(
                    binaryReader, stateObject.GetType(), stateObject
                );
                return stateObject;
            });

            stateObjectLists.Add(stateObjects);
        }

        return stateObjectLists;
    }
}