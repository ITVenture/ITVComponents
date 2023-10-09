using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ITVComponents.Plugins;

namespace ITVComponents.WebCoreToolkit.WebPlugins
{
    /// <summary>
    /// Provides a PluginFactory holding all plugins that are valid for the current scope
    /// </summary>
    public interface IWebPluginHelper:IDisposable
    {
        /// <summary>
        /// Initializes the PluginFactory
        /// </summary>
        /// <returns>the initialized factory</returns>
        IPluginFactory GetFactory();

        /// <summary>
        /// Initializes the PluginFactory
        /// </summary>
        /// <param name="explicitPluginScope">the scope that must be explicitly used for loading plugins and constants</param>
        /// <returns>the initialized factory</returns>
        IPluginFactory GetFactory(string explicitPluginScope);

        /// <summary>
        /// Resets the factory
        /// </summary>
        void ResetFactory();
    }
}
