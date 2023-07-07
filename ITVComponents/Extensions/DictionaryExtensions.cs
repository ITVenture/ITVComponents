using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.Extensions
{
    public static class DictionaryExtensions
    {
        public static void AddOrUpdate<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key,
            TValue initialValue, Func<KeyValuePair<TKey, TValue>, TValue> update)
        {
            if (dictionary.TryGetValue(key, out var vl))
            {
                dictionary[key] = update(new KeyValuePair<TKey, TValue>(key, vl));
            }
            else
            {
                dictionary[key] = initialValue;
            }
        }
    }
}
