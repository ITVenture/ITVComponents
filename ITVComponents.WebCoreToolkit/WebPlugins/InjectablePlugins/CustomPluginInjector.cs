using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.WebPlugins.InjectablePlugins
{
    /// <summary>
    /// Base implementation of a Plugin-injector
    /// </summary>
    /// <typeparam name="T">the Plugin-type that is created with this injector</typeparam>
    public abstract class CustomPluginInjector<T>:ICustomPluginInjector where T : class
    {
        /// <summary>
        /// Gets the name of the Plugin to create with this injector
        /// </summary>
        /// <param name="services">the service-collection that contains services required to estimate the name</param>
        /// <param name="prefixWithArea">indicates whether to check for a prefix with the given area-prefix</param>
        /// <returns>the estimated proxy-name</returns>
        protected abstract string GetPluginUniqueName(IServiceProvider services, bool prefixWithArea);

        /// <summary>
        /// Creates a Plugin proxy
        /// </summary>
        /// <param name="services">the services collection providing required dependencies</param>
        /// <param name="prefixWithArea">indicates whether to check for a prefix with the given area-prefix</param>
        /// <returns>the requested plugin instance</returns>
        object ICustomPluginInjector.GetPlugin(IServiceProvider services, bool prefixWithArea)
        {
            var factoryLoader = services.GetRequiredService<IWebPluginHelper>();
            var factory = factoryLoader.GetFactory();
            return factory[GetPluginUniqueName(services, prefixWithArea)];
        }

        /// <summary>
        /// Gets the demanded plugin instance
        /// </summary>
        /// <param name="services">the DI services for the current request</param>
        /// <param name="explicitRequestedName">the name of the required plugin</param>
        /// <returns>the demanded plugin instance</returns>
        object ICustomPluginInjector.GetPlugin(IServiceProvider services, string explicitRequestedName)
        {
            var factoryLoader = services.GetRequiredService<IWebPluginHelper>();
            var factory = factoryLoader.GetFactory();
            return factory[explicitRequestedName];
        }

        /// <summary>
        /// Gets the demanded plugin instance
        /// </summary>
        /// <param name="services">the DI services for the current request</param>
        /// <param name="explicitRequestedName">the name of the required plugin</param>
        /// <returns>the demanded plugin instance</returns>
        public virtual T GetPluginInstance(IServiceProvider services, string explicitRequestedName)
        {
            return (T)((ICustomPluginInjector)this).GetPlugin(services, explicitRequestedName);
        }

        /// <summary>
        /// Creates a Plugin
        /// </summary>
        /// <param name="services">the services collection providing required dependencies</param>
        /// <param name="prefixWithArea">indicates whether to check for a prefix with the given area-prefix</param>
        /// <returns>the requested Plugin instance</returns>
        public virtual T GetPluginInstance(IServiceProvider services, bool prefixWithArea)
        {
            return (T)((ICustomPluginInjector)this).GetPlugin(services, prefixWithArea);
        }
    }
}
