using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ITVComponents.DataExchange.Configuration
{
    [Serializable]
    public class QueryConfiguration
    {
        /// <summary>
        /// Gets or sets the Query text of this quey
        /// </summary>
        public string Query { get; set; }

        /// <summary>
        /// Gets or sets the Name of this Query
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the SourceDataConnection on which to execute the configured query
        /// </summary>
        public string Source { get; set; }

        /// <summary>
        /// Gets the Children of this query
        /// </summary>
        public QueryConfigurationCollection Children { get; set; } = new QueryConfigurationCollection();

        /// <summary>
        /// Gets the parameters of this Query
        /// </summary>
        public QueryParameterCollection Parameters { get; set; } = new QueryParameterCollection();

        /// <summary>
        /// Gets the Delivery targets of this query
        /// </summary>
        public DeliveryTargetCollection Targets { get; set; } = new DeliveryTargetCollection();

        /// <summary>Returns a string that represents the current object.</summary>
        /// <returns>A string that represents the current object.</returns>
        /// <filterpriority>2</filterpriority>
        public override string ToString()
        {
            if (!string.IsNullOrEmpty(Name))
            {
                return $"{Name} (@{Source}, {Parameters.Count} parameters, {Targets.Count} targets, {Children.Count} children)";
            }

            return base.ToString();
        }
    }
}
