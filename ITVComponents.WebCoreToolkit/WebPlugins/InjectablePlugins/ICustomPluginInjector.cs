using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Plugins;
using Microsoft.AspNetCore.Mvc.ModelBinding.Binders;

namespace ITVComponents.WebCoreToolkit.WebPlugins.InjectablePlugins
{
    /// <summary>
    /// Custom Plugin Injector
    /// </summary>
    public interface ICustomPluginInjector
    {
        /// <summary>
        /// Gets the demanded plugin instance
        /// </summary>
        /// <param name="services">the DI services for the  current request</param>
        /// <param name="prefixWithArea">indicates whether to check for a prefix with the given area-prefix</param>
        /// <returns>the demanded plugin instance</returns>
        IPlugin GetPlugin(IServiceProvider services, bool prefixWithArea);

        /// <summary>
        /// Gets the demanded plugin instance
        /// </summary>
        /// <param name="services">the DI services for the current request</param>
        /// <param name="explicitRequestedName">the name of the required plugin</param>
        /// <returns>the demanded plugin instance</returns>
        IPlugin GetPlugin(IServiceProvider services, string explicitRequestedName);
    }
}
