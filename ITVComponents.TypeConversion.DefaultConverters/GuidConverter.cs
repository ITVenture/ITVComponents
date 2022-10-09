using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Logging;

namespace ITVComponents.TypeConversion.DefaultConverters
{
    /// <summary>
    /// A Default instance of a String-to-GUID-Converter
    /// </summary>
    public class GuidConverter:TypeConversionPlugin
    {
        /// <summary>
        /// Gets a value indicating whether this converter is capable of converting the given value to the target type
        /// </summary>
        /// <param name="value">the value that nees to be converted</param>
        /// <param name="targetType">the target type into which the value must be converted</param>
        /// <returns>a value indicating whether the requested conversion can be done with this converter</returns>
        public override bool CapableFor(object value, Type targetType)
        {
            return value is string && targetType == typeof(Guid);
        }

        /// <summary>
        /// Tries to convert the given value into the target type and returns a value indicating whether the conversion was successful
        /// </summary>
        /// <param name="value">the value that needs to be converted</param>
        /// <param name="targetType">the requested target type</param>
        /// <param name="result">the result if the conversion was successful</param>
        /// <returns>a value indicating whether the conversion was successful</returns>
        public override bool TryConvert(object value, Type targetType, out object result)
        {
            result = null;
            try
            {
                result = Guid.Parse(value as string);
                return true;
            }
            catch (Exception ex)
            {
                LogEnvironment.LogEvent($"Conversion failed for {value} ({ex.Message})",LogSeverity.Error);
            }

            return false;
        }
    }
}
