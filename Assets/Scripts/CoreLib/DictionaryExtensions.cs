using System.Collections.Generic;

public static class DictionaryExtensions
{
    public static TValue GetValueOrDefault<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key)
    {
        TValue value;
        dictionary.TryGetValue(key, out value);

        return value;
    }
}