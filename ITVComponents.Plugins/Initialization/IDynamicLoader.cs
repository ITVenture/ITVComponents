using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ITVComponents.Helpers;

namespace ITVComponents.Plugins.Initialization
{
    public interface IDynamicLoader : IPlugin
    {
        /// <summary>
        /// Loads dynamic assemblies that are required for a specific application
        /// </summary>
        IEnumerable<string> LoadDynamicAssemblies();

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
        void GetGenericParams(string uniqueName, List<GenericTypeArgument> genericTypeArguments, Dictionary<string, object> customVariables, IStringFormatProvider formatter);
    }
}
