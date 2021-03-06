using System.Collections.Generic;
using System.Linq;
using ITVComponents.WebCoreToolkit.Models;
using ITVComponents.WebCoreToolkit.WebPlugins;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.WebPlugins
{
    internal class DbPluginsSelector:IWebPluginsSelector
    {
        private readonly IBaseTenantContext securityContext;

        /// <summary>
        /// Initializes a new instance of hte DbPluginsSelector class
        /// </summary>
        /// <param name="securityContext">the injected security-db-context</param>
        public DbPluginsSelector(IBaseTenantContext securityContext)
        {
            this.securityContext = securityContext;
        }

        /// <summary>
        /// Get all Plugins that have a Startup-constructor
        /// </summary>
        /// <returns></returns>
        public IEnumerable<WebPlugin> GetStartupPlugins()
        {
            return from p in securityContext.WebPlugins where !string.IsNullOrEmpty(p.StartupRegistrationConstructor) orderby p.UniqueName select p;
        }

        /// <summary>
        /// Gets the definition of a specific UniqueName
        /// </summary>
        /// <param name="uniqueName">the uniqueName for the desired plugin-definition</param>
        /// <returns>a WebPlugin definition that can be processed by the underlying factory</returns>
        public WebPlugin GetPlugin(string uniqueName)
        {
            return securityContext.WebPlugins.FirstOrDefault(n => n.UniqueName == uniqueName);
        }

        /// <summary>
        /// Gets a list of all Plugins that have the AutlLoad-flag set
        /// </summary>
        /// <returns>returns a list with auto-load Plugins</returns>
        public IEnumerable<WebPlugin> GetAutoLoadPlugins()
        {
            return from p in securityContext.WebPlugins where !string.IsNullOrEmpty(p.Constructor) && p.AutoLoad select p;
        }

        /// <summary>
        /// Copnfigures a Web-Plugin. This only works, when the Plugin-Configuration is writable
        /// </summary>
        /// <param name="pi">the plugin-member to modify</param>
        public void ConfigurePlugin(WebPlugin pi)
        {
            securityContext.SaveChanges();
        }

        /// <summary>
        /// Gets the generic arguments for the specified plugin
        /// </summary>
        /// <param name="uniqueName">the name of the plugin for which to get the generic arguments</param>
        /// <returns>a list of parametetrs for this plugin</returns>
        public IEnumerable<WebPluginGenericParam> GetGenericParameters(string uniqueName)
        {
            return from p in securityContext.GenericPluginParams where p.Plugin.UniqueName == uniqueName select p;
        }
    }
}
