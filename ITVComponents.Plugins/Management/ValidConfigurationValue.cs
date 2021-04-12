using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ITVComponents.Plugins.Management
{
    /// <summary>
    /// Contains a valid value for a specific configurationparameter
    /// </summary>
    public class ValidConfigurationValue
    {
        /// <summary>
        /// The value of this configurationValue instance
        /// </summary>
        public object Value { get; set; }

        /// <summary>
        /// A description indicating what this value does
        /// </summary>
        public string Description { get; set; }
    }
}
