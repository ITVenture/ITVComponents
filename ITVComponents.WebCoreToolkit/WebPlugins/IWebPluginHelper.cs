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
        PluginFactory GetFactory();

        /// <summary>
        /// Initializes the PluginFactory
        /// </summary>
        /// <param name="explicitPluginScope">the scope that must be explicitly used for loading plugins and constants</param>
        /// <returns>the initialized factory</returns>
        PluginFactory GetFactory(string explicitPluginScope);

        /// <summary>
        /// Opens a fresh per-operation plugin scope (see <see cref="PluginFactory.NewScope"/>). Plugins loaded
        /// through the returned factory — and any scope-owned dependencies (e.g. a per-operation DbContext,
        /// configured via <c>FactoryOptions.AddDependency(..., disposeWithScope: true)</c>) — are constructed for
        /// this scope and disposed when the returned <see cref="IPluginFactory"/> is disposed. Use within a
        /// <c>using</c> for a unit of work; nothing leaks past the scope.
        /// </summary>
        IPluginFactory CreateOperationScope();

        /// <summary>
        /// Like <see cref="CreateOperationScope()"/> but pins the given explicit plugin permission-scope.
        /// </summary>
        IPluginFactory CreateOperationScope(string explicitPluginScope);

        /// <summary>
        /// Resets the factory
        /// </summary>
        void ResetFactory();
    }
}
