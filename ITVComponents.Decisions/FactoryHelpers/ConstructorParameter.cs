using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.Decisions.FactoryHelpers
{
    public class ConstructorParameter
    {
        /// <summary>
        /// The value that is passed to the Constructor
        /// </summary>
        private readonly object value;

        /// <summary>
        /// The expected Parametertype
        /// </summary>
        private readonly Type expectedType;

        /// <summary>
        /// Initializes a new instance of the ConstructorParameter class
        /// </summary>
        /// <param name="value">the value to pass to the constructor. The Value must not be null. Use this overload only, if you provide the exact type that is expected.</param>
        public ConstructorParameter(object value) : this(value, value.GetType())
        {
        }

        /// <summary>
        /// Initializes a new instance of the ConstructorParameter class
        /// </summary>
        /// <param name="value">the value to pass to the constructor</param>
        /// <param name="expectedType">the Type that is expected by the constructor</param>
        public ConstructorParameter(object value, Type expectedType)
        {
            this.value = value;
            this.expectedType = expectedType;
        }

        /// <summary>
        /// Gets the Type that is expected by the Constructor
        /// </summary>
        public Type ExpectedType { get { return expectedType; } }

        /// <summary>
        /// Gets the value for the Constructor
        /// </summary>
        public object Value { get { return value; } }
    }
}
