using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using Force.DeepCloner;
using Newtonsoft.Json;

public static class ObjectExtensions
{
    public static T DeepCopy<T>(T obj)
    {
        UnityEngine.Profiling.Profiler.BeginSample("DeepCopy");
        var copy = obj.DeepClone();
        UnityEngine.Profiling.Profiler.EndSample();

        return copy;
    }
    public static T DeepCopyWithBinaryFormatter<T>(T obj)
    {
        using (var memoryStream = new MemoryStream())
        {
            var binaryFormatter = new BinaryFormatter();
            binaryFormatter.Serialize(memoryStream, obj);
            memoryStream.Position = 0;

            var copiedObj = (T)binaryFormatter.Deserialize(memoryStream);
            return copiedObj;
        }
    }
    public static T DeepCopyWithJsonSerialization<T>(T obj)
    {
        var serializedObj = JsonConvert.SerializeObject(obj, jsonSerializerSettings);
        return JsonConvert.DeserializeObject<T>(serializedObj, jsonSerializerSettings);
    }

    private static JsonSerializerSettings jsonSerializerSettings = new JsonSerializerSettings
    {
        ContractResolver = new CustomJsonContractResolver()
    };
}