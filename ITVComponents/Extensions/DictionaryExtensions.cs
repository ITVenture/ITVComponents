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

        public static TValue GetOrInsert<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key,
            Func<TKey, TValue> create)
        {
            if (dictionary.ContainsKey(key))
            {
                return dictionary[key];
            }

            TValue retVal = create(key);
            dictionary.Add(key, retVal);
            return retVal;
        }

        public static Dictionary<string, object> ExtendDictionary(this Dictionary<string, object> source,
            Dictionary<string, object> extendee)
        {
            var retVal = extendee.Count == 0 ? extendee : new Dictionary<string, object>();
            if (retVal != extendee)
            {
                foreach (var kvp in extendee)
                {
                    retVal.Add(kvp.Key, kvp.Value);
                }
            }

            foreach (var key in source.Keys)
            {
                if (!retVal.ContainsKey(key))
                {
                    retVal.Add(key, source[key]);
                }
            }

            return retVal;
        }
    }
}
