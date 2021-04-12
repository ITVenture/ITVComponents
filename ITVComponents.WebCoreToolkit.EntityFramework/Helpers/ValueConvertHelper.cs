using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.TypeConversion;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Helpers
{
    public static class ValueConvertHelper
    {
        /// <summary>
        /// Basic functionality for Type-Conversions including GUID
        /// </summary>
        /// <typeparam name="T">the target type to convert to</typeparam>
        /// <param name="srcValue">the source value to which a convert must be performed</param>
        /// <returns>the requested value in the target-type</returns>
        public static T ChangeType<T>(string srcValue)
        {
            Type t = typeof(T);
            if (t == typeof(Guid))
            {
                object obj = Guid.Parse(srcValue);
                return (T) obj;
            }

            return (T)TypeConverter.Convert(srcValue, t);
        }

        /// <summary>
        /// Basic functionality for Type-Conversions including Guid. Returns null, when the conversion fails.
        /// </summary>
        /// <typeparam name="T">the target type to convert to</typeparam>
        /// <param name="srcValue">the source value from which to convert</param>
        /// <returns>the value in the requested type or null</returns>
        public static T? TryChangeType<T>(string srcValue) where T:struct
        {
            try
            {
                return ChangeType<T>(srcValue);
            }
            catch
            {
            }

            return null;
        }
    }
}
