using ITVComponents.Plugins.DatabaseDrivenConfiguration.Config;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.Plugins.DatabaseDrivenConfiguration
{
    public class DatabaseLoaderSettings
    {
        /// <summary>
        /// Gets or sets the available Loader-Configurations for the databasedriven PluginLoader
        /// </summary>
        public LoaderConfigurationCollection LoaderConfigurations { get; set; } = new LoaderConfigurationCollection();
    }
}
