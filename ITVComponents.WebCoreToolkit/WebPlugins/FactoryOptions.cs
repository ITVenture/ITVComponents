using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ITVComponents.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.WebPlugins
{
    /// <summary>
    /// Enables external objects ot configure a created PluginFactory with further fixed dependencies
    /// </summary>
    public class FactoryOptions
    {
        private Dictionary<string, Func<IServiceProvider,object>> dependencies = new Dictionary<string, Func<IServiceProvider, object>>();

        /// <summary>
        /// Adds a dependency that must be accessible from the pluginfactory as a parameter
        /// </summary>
        /// <param name="name">the name of the dependency</param>
        /// <param name="dependency">the injected value of the dependency</param>
        public void AddDependency(string name, Func<IServiceProvider, object> dependency)
        {
            dependencies.Add(name, dependency);
        }

        /// <summary>
        /// Configures the target factory with the provided parameters
        /// </summary>
        /// <param name="name">the configured name of the required dependency</param>
        /// <param name="services">the services collection that enables the pluginFactory to get a specific dependency</param>
        internal object GetDependency(string name, IServiceProvider services)
        {
            if (dependencies.ContainsKey(name))
            {
                return dependencies[name](services);
            }

            return null;
        }
    }
}
