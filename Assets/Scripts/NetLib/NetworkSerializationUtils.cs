using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public static class NetworkSerializationUtils
{
    public static byte[] SerializeWithType(INetworkMessage networkMessage)
    {
        var memoryStream = new MemoryStream();
        var binaryWriter = new BinaryWriter(memoryStream);

        binaryWriter.Write((byte)networkMessage.GetMessageType());
        networkMessage.Serialize(binaryWriter);

        return memoryStream.ToArray();
    }

    public static byte[] SerializeRpcCall(RpcInfo rpcInfo, object argumentsObj)
    {
        var argumentsType = argumentsObj.GetType();
        var argumentProperties = argumentsType.GetProperties();

        Debug.Assert(argumentProperties.Length == rpcInfo.ParameterTypes.Length);

        var arguments = rpcInfo.ParameterTypes
            .Select((parameterType, parameterIndex) =>
            {
                var argumentProperty = argumentProperties.First(argField =>
                    argField.Name == rpcInfo.ParameterNames[parameterIndex]
                );
                return argumentProperty.GetValue(argumentsObj);
            })
            .ToArray();

        return SerializeRpcCall(rpcInfo, arguments);
    }
    public static byte[] SerializeRpcCall(RpcInfo rpcInfo, params object[] rpcArguments)
    {
        Debug.Assert(rpcArguments.Length == rpcInfo.ParameterTypes.Length);

        var memoryStream = new MemoryStream();
        var binaryWriter = new BinaryWriter(memoryStream);

        binaryWriter.Write(rpcInfo.Id);

        for (var i = 0; i < rpcArguments.Length; i++)
        {
            var argument = rpcArguments[i];
            var argumentType = argument.GetType();
            var parameterType = rpcInfo.ParameterTypes[i];

            Debug.Assert(argumentType.IsEquivalentTo(parameterType));

            Serialize(binaryWriter, argument);
        }

        return memoryStream.ToArray();
    }
    public static object[] DeserializeRpcCallArguments(RpcInfo rpcInfo, BinaryReader reader)
    {
        return rpcInfo.ParameterTypes
            .Select(parameterType => Deserialize(reader, parameterType))
            .ToArray();
    }

    public static void Serialize(BinaryWriter writer, object obj)
    {
        if (obj is bool)
        {
            writer.Write((bool)obj);
        }
        else if (obj is sbyte)
        {
            writer.Write((sbyte)obj);
        }
        else if (obj is byte)
        {
            writer.Write((byte)obj);
        }
        else if (obj is ushort)
        {
            writer.Write((ushort)obj);
        }
        else if (obj is short)
        {
            writer.Write((short)obj);
        }
        else if (obj is uint)
        {
            writer.Write((uint)obj);
        }
        else if (obj is int)
        {
            writer.Write((int)obj);
        }
        else if (obj is ulong)
        {
            writer.Write((ulong)obj);
        }
        else if (obj is long)
        {
            writer.Write((long)obj);
        }
        else if (obj is float)
        {
            writer.Write((float)obj);
        }
        else if (obj is double)
        {
            writer.Write((double)obj);
        }
        else if (obj is decimal)
        {
            writer.Write((decimal)obj);
        }
        else if (obj is char)
        {
            writer.Write((char)obj);
        }
        else if (obj is string)
        {
            writer.Write((string)obj);
        }
        else if (obj is Vector2)
        {
            Serialize(writer, (Vector2)obj);
        }
        else if (obj is Vector3)
        {
            Serialize(writer, (Vector3)obj);
        }
        else
        {
            var objType = obj.GetType();

            var objFields = objType.GetFields();
            foreach (var objField in objFields)
            {
                Serialize(writer, objField.GetValue(obj));
            }

            var objProperties = objType.GetProperties();
            foreach (var objProperty in objProperties)
            {
                Serialize(writer, objProperty.GetValue(obj));
            }
        }
    }
    public static object Deserialize(BinaryReader reader, Type type)
    {
        if (type == typeof(bool))
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
        else
        {
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