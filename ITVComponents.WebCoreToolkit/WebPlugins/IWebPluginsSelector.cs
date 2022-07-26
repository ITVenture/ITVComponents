using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Models;

namespace ITVComponents.WebCoreToolkit.WebPlugins
{
    /// <summary>
    /// Provides a strategy to get the current plugin-configuration 
    /// </summary>
    public interface IWebPluginsSelector
    {
        /// <summary>
        /// Gets or sets the explicit scope in which the plugins must be loaded. When this value is not set, the default is used.
        /// </summary>
        string ExplicitPluginPermissionScope { get; protected internal set; }

        /// <summary>
        /// Indicates whether this PluginSelector is currently able to differ plugins between permission-scopes
        /// </summary>
        bool ExplicitScopeSupported { get; }

        /// <summary>
        /// Get all Plugins that have a Startup-constructor
        /// </summary>
        /// <returns></returns>
        IEnumerable<WebPlugin> GetStartupPlugins();

        /// <summary>
        /// Gets the definition of a specific UniqueName
        /// </summary>
        /// <param name="uniqueName">the uniqueName for the desired plugin-definition</param>
        /// <returns>a WebPlugin definition that can be processed by the underlying factory</returns>
        WebPlugin GetPlugin(string uniqueName);

        /// <summary>
        /// Gets a list of all Plugins that have the AutlLoad-flag set
        /// </summary>
        /// <returns>returns a list with auto-load Plugins</returns>
        IEnumerable<WebPlugin> GetAutoLoadPlugins();

        /// <summary>
        /// Copnfigures a Web-Plugin. This only works, when the Plugin-Configuration is writable
        /// </summary>
        /// <param name="pi">the plugin-member to modify</param>
        void ConfigurePlugin(WebPlugin pi);

        /// <summary>
        /// Gets the generic arguments for the specified plugin
        /// </summary>
        /// <param name="uniqueName">the name of the plugin for which to get the generic arguments</param>
        /// <returns>a list of parametetrs for this plugin</returns>
        IEnumerable<WebPluginGenericParam> GetGenericParameters(string uniqueName);
    }
}
