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
    public abstract class CustomPluginInjector<T>:ICustomPluginInjector where T:class,IPlugin
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
        IPlugin ICustomPluginInjector.GetPlugin(IServiceProvider services, bool prefixWithArea)
        {
            var factoryLoader = services.GetRequiredService<IWebPluginHelper>();
            var factory = factoryLoader.GetFactory();
            return factory[GetPluginUniqueName(services, prefixWithArea),true];
        }

        /// <summary>
        /// Gets the demanded plugin instance
        /// </summary>
        /// <param name="services">the DI services for the current request</param>
        /// <param name="explicitRequestedName">the name of the required plugin</param>
        /// <returns>the demanded plugin instance</returns>
        IPlugin ICustomPluginInjector.GetPlugin(IServiceProvider services, string explicitRequestedName)
        {
            var factoryLoader = services.GetRequiredService<IWebPluginHelper>();
            var factory = factoryLoader.GetFactory();
            return factory[explicitRequestedName, true];
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

        /// <summary>
        /// Loest das Plugin aus einem BEREITS GEOEFFNETEN Scope auf (statt aus dem CurrentScope der Factory) -
        /// fuer den frischen, aufrufer-besessenen Ladeweg (<see cref="IFreshInjectablePlugin{T}"/>). Nutzt
        /// dieselbe Namens-/Scope-Logik (<see cref="GetPluginUniqueName"/>) wie der regulaere Pfad.
        /// </summary>
        /// <param name="services">die DI-Services der aktuellen Anfrage</param>
        /// <param name="scope">der frische Lade-Scope, aus dem geladen wird</param>
        /// <param name="prefixWithArea">ob mit Area-Prefix gesucht werden soll</param>
        /// <returns>die aufgeloeste Plugin-Instanz</returns>
        public virtual T GetPluginInstance(IServiceProvider services, IPluginFactory scope, bool prefixWithArea)
        {
            return (T)scope[GetPluginUniqueName(services, prefixWithArea), true];
        }
    }
}
