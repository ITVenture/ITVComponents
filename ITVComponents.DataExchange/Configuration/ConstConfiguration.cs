using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ITVComponents.DataExchange.Configuration
{
    [Serializable]
    public class ConstConfiguration
    {
        /// <summary>
        /// Gets or sets the name of the configured Const
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the ValueExpression of the configured const
        /// </summary>
        public string ValueExpression { get; set; }

        /// <summary>
        /// Gets or sets the Type of this constant. Default-Value is SingleExpression
        /// </summary>
        public ConstType ConstType { get; set; } = ConstType.SingleExpression;

        /// <summary>Returns a string that represents the current object.</summary>
        /// <returns>A string that represents the current object.</returns>
        /// <filterpriority>2</filterpriority>
        public override string ToString()
        {
            if (!string.IsNullOrEmpty(Name))
            {
                return $"{Name} ({ConstType}, {ValueExpression})";
            }

            return base.ToString();
        }
    }

    public enum ConstType
    {
        /// <summary>
        /// The Const uses a single expression
        /// </summary>
        SingleExpression,

        /// <summary>
        /// The const evaluates in a block and contains a return statement
        /// </summary>
        ExpressionBlock
    }
}
