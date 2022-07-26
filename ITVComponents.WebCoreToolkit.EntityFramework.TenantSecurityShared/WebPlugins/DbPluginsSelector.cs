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
        /// Gets or sets the explicit scope in which the plugins must be loaded. When this value is not set, the default is used.
        /// </summary>
        protected internal string ExplicitPluginPermissionScope { get; set; }

        /// <summary>
        /// Gets or sets the explicit scope in which the plugins must be loaded. When this value is not set, the default is used.
        /// </summary>
        string IWebPluginsSelector.ExplicitPluginPermissionScope
        {
            get => this.ExplicitPluginPermissionScope;
            set => this.ExplicitPluginPermissionScope = value;
        }

        /// <summary>
        /// Indicates whether this PluginSelector is currently able to differ plugins between permission-scopes
        /// </summary>
        public bool ExplicitScopeSupported => !securityContext.FilterAvailable || securityContext.ShowAllTenants;

        /// <summary>
        /// Get all Plugins that have a Startup-constructor
        /// </summary>
        /// <returns></returns>
        public IEnumerable<WebPlugin> GetStartupPlugins()
        {
            if (securityContext.FilterAvailable && !securityContext.ShowAllTenants)
            {
                return from p in securityContext.WebPlugins
                    where !string.IsNullOrEmpty(p.StartupRegistrationConstructor)
                    orderby p.UniqueName
                    select p;
            }

            if (string.IsNullOrEmpty(ExplicitPluginPermissionScope))
            {
                return from p in securityContext.WebPlugins
                    where p.TenantId == null && !string.IsNullOrEmpty(p.StartupRegistrationConstructor)
                    orderby p.UniqueName
                    select p;
            }

            return from p in securityContext.WebPlugins
                where (p.TenantId == null || p.Tenant.TenantName == ExplicitPluginPermissionScope) &&
                      !string.IsNullOrEmpty(p.StartupRegistrationConstructor)
                orderby p.UniqueName
                select p;
        }

        /// <summary>
        /// Gets the definition of a specific UniqueName
        /// </summary>
        /// <param name="uniqueName">the uniqueName for the desired plugin-definition</param>
        /// <returns>a WebPlugin definition that can be processed by the underlying factory</returns>
        public WebPlugin GetPlugin(string uniqueName)
        {
            if (securityContext.FilterAvailable && !securityContext.ShowAllTenants)
            {
                return securityContext.WebPlugins.FirstOrDefault(n => n.UniqueName == uniqueName);
            }

            if (string.IsNullOrEmpty(ExplicitPluginPermissionScope))
            {
                return securityContext.WebPlugins.FirstOrDefault(n => n.TenantId == null && n.UniqueName == uniqueName);
            }

            return securityContext.WebPlugins.FirstOrDefault(n => (n.TenantId == null || n.Tenant.TenantName == ExplicitPluginPermissionScope) && n.UniqueName == uniqueName);
        }

        /// <summary>
        /// Gets a list of all Plugins that have the AutlLoad-flag set
        /// </summary>
        /// <returns>returns a list with auto-load Plugins</returns>
        public IEnumerable<WebPlugin> GetAutoLoadPlugins()
        {
            if (securityContext.FilterAvailable && !securityContext.ShowAllTenants)
            {
                return from p in securityContext.WebPlugins
                    where !string.IsNullOrEmpty(p.Constructor) && p.AutoLoad
                       orderby p.UniqueName
                    select p;
            }

            if (string.IsNullOrEmpty(ExplicitPluginPermissionScope))
            {
                return from p in securityContext.WebPlugins
                    where p.TenantId == null && !string.IsNullOrEmpty(p.Constructor) && p.AutoLoad
                       orderby p.UniqueName
                    select p;
            }

            return from p in securityContext.WebPlugins
                where (p.TenantId == null || p.Tenant.TenantName == ExplicitPluginPermissionScope) &&
                      !string.IsNullOrEmpty(p.Constructor) && p.AutoLoad
                   orderby p.UniqueName
                select p;
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
            if (securityContext.FilterAvailable && !securityContext.ShowAllTenants)
            {
                return from p in securityContext.GenericPluginParams
                    where p.Plugin.UniqueName == uniqueName
                    select p;
            }

            if (string.IsNullOrEmpty(ExplicitPluginPermissionScope))
            {
                return from p in securityContext.GenericPluginParams
                    where p.Plugin.TenantId == null &&  p.Plugin.UniqueName == uniqueName
                    select p;
            }

            return from p in securityContext.GenericPluginParams
                where (p.Plugin.TenantId == null || p.Plugin.Tenant.TenantName == ExplicitPluginPermissionScope) && p.Plugin.UniqueName == uniqueName
                select p;
        }
    }
}
