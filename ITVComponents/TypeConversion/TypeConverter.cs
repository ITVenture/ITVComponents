using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
