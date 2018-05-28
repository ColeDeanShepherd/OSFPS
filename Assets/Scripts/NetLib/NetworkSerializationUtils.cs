using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

                    SerializeObject(binaryWriter, argument, argumentType);
                }
            }

            return memoryStream.ToArray();
        }
    }
    public static object[] DeserializeRpcCallArguments(RpcInfo rpcInfo, BinaryReader reader)
    {
        return rpcInfo.ParameterTypes
            .Select(parameterType => Deserialize(reader, parameterType))
            .ToArray();
    }

    public static Type GetSmallestIntTypeToHoldEnumValues(Type enumType)
    {
        Assert.IsTrue(enumType.IsEnum);

        var numEnumValues = enumType.GetEnumValues().Length;

        if (numEnumValues <= byte.MaxValue)
        {
            return typeof(byte);
        }
        else if (numEnumValues <= ushort.MaxValue)
        {
            return typeof(ushort);
        }
        else
        {
            return typeof(uint);
        }
    }
    public static void Serialize<T>(BinaryWriter writer, T t, bool isNullableIfReferenceType = false)
    {
        SerializeObject(writer, t, typeof(T), isNullableIfReferenceType);
    }
    public static void SerializeObject(
        BinaryWriter writer, object obj, Type overrideType = null, bool isNullableIfReferenceType = false
    )
    {
        var objType = overrideType ?? obj?.GetType();
        Assert.IsNotNull(objType);

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
        else
        {
            if (objType.IsEnum)
            {
                var smallestTypeToHoldEnumValues = GetSmallestIntTypeToHoldEnumValues(objType);
                SerializeObject(writer, Convert.ChangeType(obj, smallestTypeToHoldEnumValues));
            }
            else if (objType.IsClass || objType.IsValueType)
            {
                //Debug.Log($"Serializing type: {objType.AssemblyQualifiedName}");

                var objFields = objType.GetFields();
                foreach (var objField in objFields)
                {
                    SerializeObject(writer, objField.GetValue(obj));
                }

                var objProperties = objType.GetProperties();
                foreach (var objProperty in objProperties)
                {
                    SerializeObject(writer, objProperty.GetValue(obj));
                }
            }
            else
            {
                throw new NotImplementedException($"Cannot serialize type: {objType.AssemblyQualifiedName}");
            }
        }
    }
    public static object Deserialize(BinaryReader reader, Type type, bool isNullableIfReferenceType = false)
    {
        var nullableUnderlyingType = Nullable.GetUnderlyingType(type);
        if ((nullableUnderlyingType == null) && isNullableIfReferenceType && type.IsClass)
        {
            nullableUnderlyingType = type;
        }

        if (nullableUnderlyingType != null)
        {
            var objHasValue = reader.ReadBoolean();
            return objHasValue ? Deserialize(reader, nullableUnderlyingType) : null;
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
        else
        {
            if (type.IsEnum)
            {
                var smallestTypeToHoldEnumValues = GetSmallestIntTypeToHoldEnumValues(type);
                var enumValueAsInt = Deserialize(reader, smallestTypeToHoldEnumValues);

                return Enum.ToObject(type, enumValueAsInt);
            }
            else if (type.IsClass || type.IsValueType)
            {
                //Debug.Log($"Deserializing type: {type.AssemblyQualifiedName}");

                var result = Activator.CreateInstance(type);

                var objFields = type.GetFields();
                foreach (var field in objFields)
                {
                    field.SetValue(result, Deserialize(reader, field.FieldType));
                }

                var objProperties = type.GetProperties();
                foreach (var property in objProperties)
                {
                    property.SetValue(result, Deserialize(reader, property.PropertyType));
                }

                return result;
            }
            else
            {
                throw new NotImplementedException($"Cannot deserialize type: {type.AssemblyQualifiedName}");
            }
        }
    }
    public static T Deserialize<T>(BinaryReader reader, bool isNullableIfReferenceType = false)
    {
        return (T)Deserialize(reader, typeof(T), isNullableIfReferenceType);
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
}