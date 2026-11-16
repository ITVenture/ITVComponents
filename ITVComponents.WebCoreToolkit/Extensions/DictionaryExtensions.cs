using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Extensions
{
    public static class DictionaryExtensions
    {
        /// <summary>
        /// Picks the entry for a language from a dictionary keyed by culture (<c>{"de":…,"fr":…}</c>).
        /// </summary>
        /// <remarks>
        /// Order: the exact culture (<c>de-CH</c>), the language (<c>de</c>), the key <c>Default</c>. When none of
        /// them is there, <c>en</c>, and failing that the first entry - a text in the wrong language is better
        /// than none, and far better than the raw record. <paramref name="defaultValue"/> is returned only for an
        /// empty dictionary. <see cref="TryTranslate{T}"/> tells the two cases apart.
        /// </remarks>
        /// <param name="dic">the entries, keyed by culture</param>
        /// <param name="language">the wanted culture</param>
        /// <param name="defaultValue">the result for an empty dictionary</param>
        public static T Translate<T>(this IDictionary<string, T> dic, string language, T defaultValue = default(T))
        {
            if (dic.TryTranslate(language, out var retVal))
            {
                return retVal;
            }

            return dic.Fallback(defaultValue);
        }

        /// <summary>
        /// Picks the entry for a language - the exact culture, the language, or the key <c>Default</c>.
        /// </summary>
        /// <param name="dic">the entries, keyed by culture</param>
        /// <param name="language">the wanted culture</param>
        /// <param name="value">the entry found, or the default of <typeparamref name="T"/></param>
        /// <returns>true when one of the three was there; false when only a fallback (see <see cref="Translate{T}"/>) could help</returns>
        public static bool TryTranslate<T>(this IDictionary<string, T> dic, string language, out T value)
        {
            if (!string.IsNullOrEmpty(language))
            {
                if (dic.TryGetValue(language, out value))
                {
                    return true;
                }

                var dash = language.IndexOf('-');
                if (dash > 0 && dic.TryGetValue(language.Substring(0, dash), out value))
                {
                    return true;
                }
            }

            return dic.TryGetValue("Default", out value);
        }

        /// <summary>
        /// The entry used when neither the language nor <c>Default</c> is there: <c>en</c>, otherwise the first one,
        /// otherwise <paramref name="defaultValue"/>.
        /// </summary>
        internal static T Fallback<T>(this IDictionary<string, T> dic, T defaultValue)
        {
            if (dic.TryGetValue("en", out var english))
            {
                return english;
            }

            return dic.Count != 0 ? dic.Values.First() : defaultValue;
        }
    }
}
