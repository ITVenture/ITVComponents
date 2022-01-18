using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Helpers;
using ITVComponents.Logging;
using ITVComponents.WebCoreToolkit.Localization;
using Microsoft.Extensions.Localization;

namespace ITVComponents.WebCoreToolkit.Extensions
{
    public static class StringLocalizerExtensions
    {
        /// <summary>
        /// Reads a resource and deserializes it to an object using a json-deserializer
        /// </summary>
        /// <typeparam name="T">the target type</typeparam>
        /// <param name="localizer">the localizer instance</param>
        /// <param name="name">the name of the resource to deserialize</param>
        /// <returns>an object of the requested type</returns>
        public static T FromJson<T>(this IStringLocalizer localizer, string name) where T : class, new()
        {
            var raw = localizer[name];
            try
            {
                return JsonHelper.FromJsonString<T>(raw);
            }
            catch (Exception ex)
            {
                LogEnvironment.LogDebugEvent($"Error translating value to object: {ex.Message}", LogSeverity.Error);
            }

            return new T();
        }

        /// <summary>
        /// Reads a formattable resource and deserializes it to an object using a json-deserializer
        /// </summary>
        /// <typeparam name="T">the target-type</typeparam>
        /// <param name="localizer">the localizer instance</param>
        /// <param name="name">the name of the resource</param>
        /// <param name="arguments">string-format arguments. If the type implements IFormattableLocalizationObject, the resource does not have to be formatted to be used with string-format</param>
        /// <returns>an instance of the requested type.</returns>
        public static T FromJson<T>(this IStringLocalizer localizer, string name, params object[] arguments) where T : class, new()
        {
            var isformattable = typeof(IFormattableLocalizationObject).IsAssignableFrom(typeof(T));
            var raw = !isformattable ? localizer[name, arguments] : localizer[name];
            try
            {
                T retVal = JsonHelper.FromJsonString<T>(raw);
                if (isformattable && retVal is IFormattableLocalizationObject flo)
                {
                    flo.FormatProperties(arguments);
                }

                return retVal;
            }
            catch (Exception ex)
            {
                LogEnvironment.LogDebugEvent($"Error translating value to object: {ex.Message}", LogSeverity.Error);
            }



            return new T();
        }
    }
}
