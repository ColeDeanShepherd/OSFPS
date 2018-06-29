using System;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Unity.Mathematics;
using UnityEngine;

public class CustomJsonContractResolver : DefaultContractResolver
{
    protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
    {
        var property = base.CreateProperty(member, memberSerialization);

        if ((property.DeclaringType == typeof(Vector2)) || (property.DeclaringType == typeof(float2)))
        {
            var shouldSerialize =
                (property.UnderlyingName == "x") ||
                (property.UnderlyingName == "y");
            property.ShouldSerialize = x => shouldSerialize;
            property.ShouldDeserialize = x => shouldSerialize;
        }
        else if((property.DeclaringType == typeof(Vector3)) || (property.DeclaringType == typeof(float3)))
        {
            var shouldSerialize =
                (property.UnderlyingName == "x") ||
                (property.UnderlyingName == "y") ||
                (property.UnderlyingName == "z");
            property.ShouldSerialize = x => shouldSerialize;
            property.ShouldDeserialize = x => shouldSerialize;
        }
        else if((property.DeclaringType == typeof(Vector4)) || (property.DeclaringType == typeof(float4)))
        {
            var shouldSerialize =
                (property.UnderlyingName == "x") ||
                (property.UnderlyingName == "y") ||
                (property.UnderlyingName == "z") ||
                (property.UnderlyingName == "w");
            property.ShouldSerialize = x => shouldSerialize;
            property.ShouldDeserialize = x => shouldSerialize;
        }

        return property;
    }
}