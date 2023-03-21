using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ITVComponents.TypeConversion
{
    public static class TypeConverter
    {
        /// <summary>
        /// Holds all available converters
        /// </summary>
        private static List<TypeConversionProvider> availableConverters = new List<TypeConversionProvider>();
        
        /// <summary>
        /// Converts a value to a differed type
        /// </summary>
        /// <param name="value">the value that requires conversion</param>
        /// <param name="targetType">the target-type into which the value needs to be converted</param>
        /// <returns>the converted value in the requested type</returns>
        public static object Convert(object value, Type targetType)
        {
            if ((value == null && targetType.IsClass) || (value != null && value.GetType().IsSubclassOf(targetType)))
            {
                return value;
            }

            var converters = Snapshot();
            var converter = converters.FirstOrDefault(n => n.CapableFor(value, targetType));
            object result = null;
            if (converter?.TryConvert(value, targetType, out result)??false)
            {
                return result;
            }
            
            return System.Convert.ChangeType(value, targetType);
        }

        /// <summary>
        /// Converts a value to a differed type
        /// </summary>
        /// <param name="value">the value that requires conversion</param>
        /// <param name="targetType">the target-type into which the value needs to be converted</param>
        /// <returns>the converted value in the requested type</returns>
        public static object TryConvert(object value, Type targetType)
        {
            if ((value == null && targetType.IsClass) || (value != null && value.GetType().IsSubclassOf(targetType)))
            {
                return value;
            }

            var converters = Snapshot();
            var converter = converters.FirstOrDefault(n => n.CapableFor(value, targetType));
            object result = null;
            if (value != null)
            {
                if (converter?.TryConvert(value, targetType, out result) ?? false)
                {
                    return result;
                }

                try
                {
                    return System.Convert.ChangeType(value, targetType);
                }
                catch
                {
                }
            }

            return null;
        }

        /// <summary>
        /// Converts a value to a differed type
        /// </summary>
        /// <param name="value">the value that requires conversion</param>
        /// <param name="targetType">the target-type into which the value needs to be converted</param>
        /// <param name="result">the result of the conversion</param>
        /// <returns>the converted value in the requested type</returns>
        public static bool TryConvert(object value, Type targetType, out object result)
        {
            if ((value == null && targetType.IsClass) || (value != null && value.GetType().IsSubclassOf(targetType)))
            {
                result = value;
                return true;
            }

            var converters = Snapshot();
            var converter = converters.FirstOrDefault(n => n.CapableFor(value, targetType));
            if (value != null)
            {
                if (converter != null && converter.TryConvert(value, targetType, out result))
                {
                    return true;
                }

                try
                {
                    result = System.Convert.ChangeType(value, targetType);
                    return true;
                }
                catch
                {
                    result = null;
                    return false;
                }
            }

            result = null;
            return targetType.IsClass ||
                   (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(Nullable<>));
        }

        /// <summary>
        /// Registers a Conversion-Provider as available Converter instance
        /// </summary>
        /// <param name="converter">the converter that is capable for converting specific values</param>
        internal static void RegisterConverter(TypeConversionProvider converter)
        {
            lock (availableConverters)
            {
                availableConverters.Add(converter);
            }
        }
        
        /// <summary>
        /// Creates a snapshot of the registered providers
        /// </summary>
        /// <returns>an array containing all registered converters</returns>
        private static TypeConversionProvider[] Snapshot()
        {
            lock (availableConverters)
            {
                return availableConverters.ToArray();
            }
        }
    }
}
