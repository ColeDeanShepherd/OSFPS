using Newtonsoft.Json;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

public static class ObjectExtensions
{
    public static T DeepCopy<T>(T obj)
    {
        using (var memoryStream = new MemoryStream())
        {
            var binaryFormatter = new BinaryFormatter();
            binaryFormatter.Serialize(memoryStream, obj);
            memoryStream.Position = 0;

            return (T)binaryFormatter.Deserialize(memoryStream);
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