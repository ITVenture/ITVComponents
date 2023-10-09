using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ITVComponents.Plugins.Management
{
    /// <summary>
    /// Provides 
    /// </summary>
    public interface IRuntimeConfigurable
    {
        /// <summary>
        /// Gets a set of runtime-configurable settings for this RuntimeConfigurable object
        /// </summary>
        /// <returns>a dictionary containing configurable items and their value range</returns>
        Dictionary<string, ConfigurationDefinition> GetParameters();

        /// <summary>
        /// Sets a parameter on this configurable object
        /// </summary>
        /// <param name="parameterName">the parameter that needs to be set</param>
        /// <param name="value">the new value of the provided parameterName</param>
        /// <returns>a value indicating whether the value could be set for this parameter</returns>
        bool SetParameter(string parameterName, object value);
    }
}
