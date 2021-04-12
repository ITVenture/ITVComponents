using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ITVComponents.DataExchange.Configuration
{
    [Serializable]
    public class QueryParameter
    {
        /// <summary>
        /// Gets or sets the Name of this parameter
        /// </summary>
        public string ParameterName { get; set; }

        /// <summary>
        /// Gets or sets the Expression for the parameter value
        /// </summary>
        public string ParameterExpression { get; set; }

        /// <summary>Returns a string that represents the current object.</summary>
        /// <returns>A string that represents the current object.</returns>
        /// <filterpriority>2</filterpriority>
        public override string ToString()
        {
            if (!string.IsNullOrEmpty(ParameterName))
            {
                return $"{ParameterName} ({ParameterExpression})";
            }

            return base.ToString();
        }
    }
}
