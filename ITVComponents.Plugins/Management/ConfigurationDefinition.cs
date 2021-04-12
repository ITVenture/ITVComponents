using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ITVComponents.Plugins.Management
{
    public class ConfigurationDefinition
    {
        /// <summary>
        /// Gets or sets the parametername of this ConfigurationDefinition object
        /// </summary>
        public string ParameterName { get; set; }

        /// <summary>
        /// Gets or sets the DataType that is valid for this configuration definition object
        /// </summary>
        public Type DataType { get; set; }

        /// <summary>
        /// Gets or sets a list of valid values for the current configuration definition
        /// </summary>
        public ValidConfigurationValue[] ValidValues { get; set; }
    }
}
