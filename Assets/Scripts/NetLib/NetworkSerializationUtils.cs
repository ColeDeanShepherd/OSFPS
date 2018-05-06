﻿using System;
using System.Collections.Generic;
using System.IO;
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