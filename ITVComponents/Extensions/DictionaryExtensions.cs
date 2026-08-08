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

        /// <summary>
        /// Liefert eine Kopie von <paramref name="extendee"/>, ergaenzt um alle Eintraege aus
        /// <paramref name="source"/>, die darin noch fehlen. Beide Eingaben bleiben unveraendert.
        /// </summary>
        /// <remarks>
        /// Wichtig ist das "unveraendert": frueher wurde bei leerem <paramref name="extendee"/> dessen
        /// Instanz zurueckgegeben und anschliessend beschrieben - damit landeten z.B. die Konstanten eines
        /// StringFormatProviders im Dictionary des Aufrufers, das dieser danach weiterreichte.
        /// </remarks>
        public static Dictionary<string, object> ExtendDictionary(this Dictionary<string, object> source,
            Dictionary<string, object> extendee)
        {
            var retVal = new Dictionary<string, object>(extendee);
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
