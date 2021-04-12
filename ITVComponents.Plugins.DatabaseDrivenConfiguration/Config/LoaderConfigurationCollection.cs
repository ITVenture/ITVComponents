using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ITVComponents.Plugins.Config;

namespace ITVComponents.Plugins.DatabaseDrivenConfiguration.Config
{
    /// <summary>
    /// Holds Plugin - Construction Instructions
    /// </summary>
    [Serializable]
    public class LoaderConfigurationCollection : List<LoaderConfiguration>
    {
        /// <summary>
        /// Gets the configurationitem with the given name
        /// </summary>
        /// <param name="name">the name of the requested item</param>
        /// <returns>the instance of the requested configuration item</returns>
        public LoaderConfiguration this[string name] => (from t in this where t.Name == name select t).First();
    }
}
