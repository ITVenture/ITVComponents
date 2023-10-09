using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ITVComponents.Plugins.Config;
using ITVComponents.Plugins.Model;

namespace ITVComponents.Plugins.Initialization
{
    public interface IDynamicLoader
    {
        /// <summary>
        /// Gets a list of Plugins to load for the specified initialization phase
        /// </summary>
        IEnumerable<PluginInfoModel> GetStartupPlugins(PluginInitializationPhase startupPhase);

        /// <summary>
        /// Gets a list of Plugins that needs to be initialized before a specific Plugin can be initialized
        /// </summary>
        /// <param name="uniqueName">the uniqueName of the plugin that triggered this call</param>
        /// <param name="eligibleInitializationPhase">the initialization flags that are eligible for initializing the requested plugins</param>
        /// <returns>a list of Plugins that needs to be initialized</returns>
        IEnumerable<PluginInfoModel> GetPreInitSequence(string uniqueName, PluginInitializationPhase eligibleInitializationPhase);

        /// <summary>
        /// Gets a list of Plugins that needs to be initialized after a specific Plugin was initialized
        /// </summary>
        /// <param name="uniqueName">the uniqueName of the plugin that triggered this call</param>
        /// <param name="eligibleInitializationPhase">the initialization flags that are eligible for initializing the requested plugins</param>
        /// <returns>a list of Plugins that needs to be initilizaed</returns>
        IEnumerable<PluginInfoModel> GetPostInitSequence(string uniqueName, PluginInitializationPhase eligibleInitializationPhase);

        /// <summary>
        /// Gets a value indicating whether this loader is able to provide further generic information for a specific plugin
        /// </summary>
        /// <param name="uniqueName">the plugin for which to get the generic information</param>
        /// <returns>a value indicating whether the requested plugin is known by this loader and contains generic arguments</returns>
        bool HasParamsFor(string uniqueName);

        /// <summary>
        /// Fills the generic Type-Arguments with the appropriate values
        /// </summary>
        /// <param name="uniqueName">the unique-name for which to get the generic arguments</param>
        /// <param name="genericTypeArguments">get generic arguments defined in the plugin-type</param>
        /// <param name="formatter">Exposes constants that are available also for constructors in the calling factory</param>
        /// <returns>a value indicating whether the loader contains generic arguments for the requested plugin</returns>
        void GetGenericParams(string uniqueName, List<GenericTypeArgument> genericTypeArguments, Dictionary<string, object> customVariables, IStringFormatProvider formatter, out bool knownTypeUsed);

        /// <summary>
        /// When known and accurate for the given initialization-phase, true is returned and the pluginInfoModel is given for initialization
        /// </summary>
        /// <param name="currentPhase">the current initialization phase</param>
        /// <param name="uniqueName">the unique-Name of the requested plugin</param>
        /// <param name="definition">a definition of the given plugin</param>
        /// <returns>a value indicating whether the requested plugin is configured in this dynamicSource</returns>
        bool GetPluginInfo(PluginInitializationPhase currentPhase, string uniqueName, out PluginInfoModel definition);
    }

    [Flags]
    public enum PluginInitializationPhase
    {
        None = 0,
        Startup = 1,
        Static=2,
        SingletonStatic=4,
        Singleton = 8,
        ScopeStatic= 16,
        Scope = 32,
        NoTracking = 64,
        Any = Startup|Static|SingletonStatic|Singleton|ScopeStatic|Scope|NoTracking
    }
}
