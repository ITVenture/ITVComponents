using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.WebPlugins.Model;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.WebPlugins.Options;
using ITVComponents.WebCoreToolkit.Models;
using ITVComponents.WebCoreToolkit.Security;
using ITVComponents.WebCoreToolkit.WebPlugins;
using Microsoft.Extensions.Options;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.WebPlugins
{
    internal class DbPluginsSelector<TTenant, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation> :IWebPluginsSelector
    where TTenant : Tenant 
    where TWebPlugin : WebPlugin<TTenant, TWebPlugin, TWebPluginGenericParameter>, new()
    where TWebPluginConstant : WebPluginConstant<TTenant>
    where TWebPluginGenericParameter : WebPluginGenericParameter<TTenant, TWebPlugin, TWebPluginGenericParameter>
    where TSequence : Sequence<TTenant>
    where TTenantSetting : TenantSetting<TTenant>
    where TTenantFeatureActivation : TenantFeatureActivation<TTenant>
    {
        private readonly IBaseTenantContext<TTenant, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation> securityContext;
        private readonly IPermissionScope scopeProvider;
        private readonly WebPluginBufferingOptions bufferConfig;

        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, DbPluginBufferInfo<TTenant, TWebPlugin, TWebPluginGenericParameter>>>
            bufferedPlugins = new ConcurrentDictionary<string, ConcurrentDictionary<string, DbPluginBufferInfo<TTenant, TWebPlugin, TWebPluginGenericParameter>>>();

        /// <summary>
        /// Initializes a new instance of hte DbPluginsSelector class
        /// </summary>
        /// <param name="securityContext">the injected security-db-context</param>
        public DbPluginsSelector(IBaseTenantContext<TTenant, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation> securityContext, IPermissionScope scopeProvider, IOptions<WebPluginBufferingOptions> bufferConfig)
        {
            this.securityContext = securityContext;
            this.scopeProvider = scopeProvider;
            this.bufferConfig = bufferConfig.Value;
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
            if (securityContext.FilterAvailable && !securityContext.ShowAllTenants && PluginBuffered(uniqueName, out var bufferInfo))
            {
                return bufferInfo.Plugin;
            }

            if (securityContext.FilterAvailable && !securityContext.ShowAllTenants)
            {
                return TryRegisterPlugin(uniqueName, securityContext.WebPlugins.FirstOrDefault(n => n.UniqueName == uniqueName && n.TenantId != null) ??
                                         securityContext.WebPlugins.FirstOrDefault(n => n.UniqueName == uniqueName && n.TenantId == null));
            }

            if (string.IsNullOrEmpty(ExplicitPluginPermissionScope))
            {
                return securityContext.WebPlugins.FirstOrDefault(n => n.TenantId == null && n.UniqueName == uniqueName);
            }

            return securityContext.WebPlugins.FirstOrDefault(n => (n.TenantId == null || n.Tenant.TenantName == ExplicitPluginPermissionScope) && n.UniqueName == uniqueName);
        }

        private WebPlugin TryRegisterPlugin(string uniqueName, TWebPlugin pluginData)
        {
            var dc = bufferedPlugins.GetOrAdd(scopeProvider.PermissionPrefix,
                n => new ConcurrentDictionary<string, DbPluginBufferInfo<TTenant, TWebPlugin, TWebPluginGenericParameter>>());
            dc.TryAdd(uniqueName, new DbPluginBufferInfo<TTenant, TWebPlugin, TWebPluginGenericParameter>
            {
                Created = DateTime.Now,
                Plugin = pluginData!=null?new TWebPlugin
                {
                    AutoLoad = pluginData.AutoLoad,
                    Constructor = pluginData.Constructor,
                    StartupRegistrationConstructor = pluginData.StartupRegistrationConstructor,
                    UniqueName = pluginData.UniqueName  
                }:null
            });

            return pluginData;
        }

        private bool PluginBuffered(string uniqueName, out DbPluginBufferInfo<TTenant, TWebPlugin, TWebPluginGenericParameter> bufferInfo)
        {
            var dc = bufferedPlugins.GetOrAdd(scopeProvider.PermissionPrefix,
                n => new ConcurrentDictionary<string, DbPluginBufferInfo<TTenant, TWebPlugin, TWebPluginGenericParameter>>());
            var retVal = dc.TryGetValue(uniqueName, out bufferInfo);
            if (retVal && bufferConfig.BufferDuration != 0 &&
                DateTime.Now.Subtract(bufferInfo.Created).TotalSeconds > bufferConfig.BufferDuration)
            {
                dc.Remove(uniqueName, out _);
                bufferInfo = null;
                return false;
            }

            return retVal;
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
