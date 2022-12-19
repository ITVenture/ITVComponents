using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.ExtendedFormatting;

namespace ITVComponents.Extensions
{
    public static class BasicKeyValueExtensions
    {
        /// <summary>
        /// Checks whether the given BasicKeyValueProvider instance provides some specific values / keys
        /// </summary>
        /// <param name="item">the item to examine</param>
        /// <param name="predicate">the predicate to check on all values of the item</param>
        /// <returns>a value indicating whether the given predicate was satisfied by any value</returns>
        public static bool Any(this IBasicKeyValueProvider item, Func<string, object, bool> predicate)
        {
            foreach (var key in item.Keys)
            {
                var obj = item[key];
                if (predicate(key, obj))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Checks whether the given BasicKeyValueProvider instance provides some specific values / keys
        /// </summary>
        /// <param name="item">the item to examine</param>
        /// <param name="predicate">the predicate to check on all values of the item</param>
        /// <returns>a value indicating whether the given predicate was satisfied by all values</returns>
        public static bool All(this IBasicKeyValueProvider item, Func<string, object, bool> predicate)
        {
            foreach (var key in item.Keys)
            {
                var obj = item[key];
                if (!predicate(key, obj))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
