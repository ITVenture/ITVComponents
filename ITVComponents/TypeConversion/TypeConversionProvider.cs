using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.TypeConversion
{
    /// <summary>
    /// A baseclass that can be used to implement type-conversions
    /// </summary>
    public abstract class TypeConversionProvider
    {
        /// <summary>
        /// Initializes a new instance of the TypeConversionProvider class
        /// </summary>
        protected TypeConversionProvider()
        {
            TypeConverter.RegisterConverter(this);
        }
        
        /// <summary>
        /// Gets a value indicating whether this converter is capable of converting the given value to the target type
        /// </summary>
        /// <param name="value">the value that nees to be converted</param>
        /// <param name="targetType">the target type into which the value must be converted</param>
        /// <returns>a value indicating whether the requested conversion can be done with this converter</returns>
        public abstract bool CapableFor(object value, Type targetType);

        /// <summary>
        /// Tries to convert the given value into the target type and returns a value indicating whether the conversion was successful
        /// </summary>
        /// <param name="value">the value that needs to be converted</param>
        /// <param name="targetType">the requested target type</param>
        /// <param name="result">the result if the conversion was successful</param>
        /// <returns>a value indicating whether the conversion was successful</returns>
        public abstract bool TryConvert(object value, Type targetType, out object result);
    }
}
