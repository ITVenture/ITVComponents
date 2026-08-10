using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.DependencyInjection;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models.FlatTenantModels;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.WebPlugins.Model;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.WebPlugins.Options;
using ITVComponents.WebCoreToolkit.Models;
using ITVComponents.WebCoreToolkit.Models.Comparers;
using ITVComponents.WebCoreToolkit.Security;
using ITVComponents.WebCoreToolkit.WebPlugins;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Options;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.WebPlugins
{
    internal class DbPluginsSelector<TTenant, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin> :IWebPluginsSelector
    where TTenant : Tenant 
    where TWebPlugin : WebPlugin<TTenant, TWebPlugin, TWebPluginGenericParameter>, new()
    where TWebPluginConstant : WebPluginConstant<TTenant>
    where TWebPluginGenericParameter : WebPluginGenericParameter<TTenant, TWebPlugin, TWebPluginGenericParameter>
    where TSequence : Sequence<TTenant>
    where TTenantSetting : TenantSetting<TTenant>
    where TTenantFeatureActivation : TenantFeatureActivation<TTenant>
    where TExternalOAuthService : ExternalOAuthService<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
    where TExternalOAuthServiceState : ExternalOAuthServiceState<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
    where TExternalOAuthServiceTenantLogin : ExternalOAuthServiceTenantLogin<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
    {
        private readonly IToolkitContextFactory contextFactory;
        private readonly IPermissionScope scopeProvider;
        private readonly WebPluginBufferingOptions bufferConfig;

        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, DbPluginBufferInfo/*<TTenant, TWebPlugin, TWebPluginGenericParameter>*/>>
            bufferedPlugins = new ConcurrentDictionary<string, ConcurrentDictionary<string, DbPluginBufferInfo/*<TTenant, TWebPlugin, TWebPluginGenericParameter>*/>>();

        /// <summary>
        /// Initializes a new instance of hte DbPluginsSelector class
        /// </summary>
        /// <param name="contextFactory">factory yielding a fresh per-operation security-db-context</param>
        public DbPluginsSelector(IToolkitContextFactory contextFactory, IPermissionScope scopeProvider, IOptions<WebPluginBufferingOptions> bufferConfig)
        {
            this.contextFactory = contextFactory;
            this.scopeProvider = scopeProvider;
            this.bufferConfig = bufferConfig.Value;
        }

        /// <summary>
        /// Leases a fresh per-operation security-db-context for the duration of a single operation.
        /// </summary>
        private IContextLease<IBaseTenantContext<TTenant, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, BaseTenantContextSecurityTrustConfig>> LeaseDb()
            => contextFactory.Lease<IBaseTenantContext<TTenant, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, BaseTenantContextSecurityTrustConfig>>();

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
        public bool ExplicitScopeSupported
        {
            get
            {
                using var lease = LeaseDb();
                var securityContext = lease.Context;
                return !securityContext.FilterAvailable || securityContext.ShowAllTenants;
            }
        }

        /// <summary>
        /// Get all Plugins that have a Startup-constructor
        /// </summary>
        /// <returns></returns>
        public IEnumerable<WebPlugin> GetStartupPlugins()
        {
            using var lease = LeaseDb();
            var securityContext = lease.Context;
            /*if (securityContext.FilterAvailable && !securityContext.ShowAllTenants)
            {
                return from p in securityContext.WebPlugins
                    where !string.IsNullOrEmpty(p.StartupRegistrationConstructor)
                    orderby p.UniqueName
                    select p;
            }

            if (string.IsNullOrEmpty(ExplicitPluginPermissionScope))
            {*/
                return (from p in securityContext.WebPlugins
                    where p.TenantId == null && !string.IsNullOrEmpty(p.StartupRegistrationConstructor)
                    orderby p.UniqueName
                    select p).ToList();
            /*}

            return from p in securityContext.WebPlugins
                where (p.TenantId == null || p.Tenant.TenantName == ExplicitPluginPermissionScope) &&
                      !string.IsNullOrEmpty(p.StartupRegistrationConstructor)
                orderby p.UniqueName
                select p;*/
        }

        /// <summary>
        /// Gets the definition of a specific UniqueName
        /// </summary>
        /// <param name="uniqueName">the uniqueName for the desired plugin-definition</param>
        /// <returns>a WebPlugin definition that can be processed by the underlying factory</returns>
        public WebPlugin GetPlugin(string uniqueName)
        {
            using var lease = LeaseDb();
            var securityContext = lease.Context;
            return GetPlugin(securityContext, uniqueName, out _);
        }

        private WebPlugin GetPlugin(IBaseTenantContext<TTenant, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, BaseTenantContextSecurityTrustConfig> securityContext, string uniqueName, out int? webPluginId)
        {
            if (securityContext.FilterAvailable && !securityContext.ShowAllTenants && PluginBuffered(uniqueName, out var bufferInfo))
            {
                webPluginId = bufferInfo.WebPluginId;
                return bufferInfo.Plugin;
            }

            if (securityContext.FilterAvailable && !securityContext.ShowAllTenants)
            {
                var pi = securityContext.WebPlugins.FirstOrDefault(
                             n => n.UniqueName == uniqueName && n.TenantId != null) ??
                         securityContext.WebPlugins.FirstOrDefault(
                             n => n.UniqueName == uniqueName && n.TenantId == null);
                webPluginId = pi?.WebPluginId;
                return TryRegisterPlugin(uniqueName, pi);
            }

            if (string.IsNullOrEmpty(ExplicitPluginPermissionScope))
            {
                var pi = securityContext.WebPlugins.FirstOrDefault(n => n.TenantId == null && n.UniqueName == uniqueName);
                webPluginId = pi?.WebPluginId;
                return pi;
            }

            var pret = securityContext.WebPlugins.FirstOrDefault(n => n.Tenant.TenantName == ExplicitPluginPermissionScope && n.UniqueName == uniqueName) ??
                   securityContext.WebPlugins.FirstOrDefault(n => n.TenantId == null && n.UniqueName == uniqueName);
            webPluginId = pret?.WebPluginId;
            return pret;
        }

        private WebPlugin TryRegisterPlugin(string uniqueName, TWebPlugin pluginData)
        {
            var dc = bufferedPlugins.GetOrAdd(scopeProvider.PermissionPrefix,
                n => new ConcurrentDictionary<string, DbPluginBufferInfo/*<TTenant, TWebPlugin, TWebPluginGenericParameter>*/>());
            dc.TryAdd(uniqueName, new DbPluginBufferInfo/*<TTenant, TWebPlugin, TWebPluginGenericParameter>*/
            {
                Created = DateTime.Now,
                Plugin = pluginData!=null?new WebPlugin()
                {
                    AutoLoad = pluginData.AutoLoad,
                    Constructor = pluginData.Constructor,
                    StartupRegistrationConstructor = pluginData.StartupRegistrationConstructor,
                    UniqueName = pluginData.UniqueName ,
                    Transient = pluginData.Transient
                }:null,
                WebPluginId = pluginData?.WebPluginId
            });

            return pluginData;
        }

        private bool PluginBuffered(string uniqueName, out DbPluginBufferInfo/*<TTenant, TWebPlugin, TWebPluginGenericParameter>*/ bufferInfo)
        {
            var dc = bufferedPlugins.GetOrAdd(scopeProvider.PermissionPrefix,
                n => new ConcurrentDictionary<string, DbPluginBufferInfo/*<TTenant, TWebPlugin, TWebPluginGenericParameter>*/>());
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
            using var lease = LeaseDb();
            var securityContext = lease.Context;
            if (securityContext.FilterAvailable && !securityContext.ShowAllTenants)
            {
                // AllowAnonymous nur aus den globalen Zeilen: eine Projektion liest die SPALTE, nicht den
                // Getter der Entitaet - die Mandanten-Regel muss hier also von Hand stehen.
                return (from p in securityContext.WebPlugins
                    where p.TenantId != null
                    select new WebPlugin
                    {
                        AutoLoad = p.AutoLoad, Constructor = p.Constructor,
                        StartupRegistrationConstructor = p.StartupRegistrationConstructor, UniqueName = p.UniqueName,
                        Transient = p.Transient, AllowAnonymous = false
                    }).AsEnumerable().Union((from p in securityContext.WebPlugins
                    where p.TenantId == null
                    select new WebPlugin
                    {
                        AutoLoad = p.AutoLoad, Constructor = p.Constructor,
                        StartupRegistrationConstructor = p.StartupRegistrationConstructor, UniqueName = p.UniqueName,
                        Transient = p.Transient, AllowAnonymous = p.AllowAnonymous
                    }).AsEnumerable(),
                    new WebPluginComparer()).Where(n => !string.IsNullOrEmpty(n.Constructor) && n.AutoLoad).ToList();
            }

            if (string.IsNullOrEmpty(ExplicitPluginPermissionScope))
            {
                return (from p in securityContext.WebPlugins
                    where p.TenantId == null && !string.IsNullOrEmpty(p.Constructor) && p.AutoLoad
                       orderby p.UniqueName
                    select p).ToList();
            }

            return (from p in securityContext.WebPlugins
                where p.Tenant.TenantName == ExplicitPluginPermissionScope
                select new WebPlugin
                {
                    AutoLoad = p.AutoLoad,
                    Constructor = p.Constructor,
                    StartupRegistrationConstructor = p.StartupRegistrationConstructor,
                    UniqueName = p.UniqueName,
                    Transient = p.Transient,
                    AllowAnonymous = false
                }).AsEnumerable().Union((from p in securityContext.WebPlugins
                where p.TenantId == null
                select new WebPlugin
                {
                    AutoLoad = p.AutoLoad,
                    Constructor = p.Constructor,
                    StartupRegistrationConstructor = p.StartupRegistrationConstructor,
                    UniqueName = p.UniqueName,
                    Transient = p.Transient,
                    AllowAnonymous = p.AllowAnonymous
                }).AsEnumerable(),
                new WebPluginComparer()).Where(p => !string.IsNullOrEmpty(p.Constructor) && p.AutoLoad).ToList();

            /*return from p in securityContext.WebPlugins
                where (p.TenantId == null || p.Tenant.TenantName == ExplicitPluginPermissionScope) &&
                      !string.IsNullOrEmpty(p.Constructor) && p.AutoLoad
                   orderby p.UniqueName
                select p;*/
        }

        /// <summary>
        /// Copnfigures a Web-Plugin. This only works, when the Plugin-Configuration is writable
        /// </summary>
        /// <param name="pi">the plugin-member to modify</param>
        public void ConfigurePlugin(WebPlugin pi)
        {
            using var lease = LeaseDb();
            var securityContext = lease.Context;
            securityContext.SaveChanges();
        }

        /// <summary>
        /// Gets the generic arguments for the specified plugin
        /// </summary>
        /// <param name="uniqueName">the name of the plugin for which to get the generic arguments</param>
        /// <returns>a list of parametetrs for this plugin</returns>
        public IEnumerable<WebPluginGenericParam> GetGenericParameters(string uniqueName)
        {
            using var lease = LeaseDb();
            var securityContext = lease.Context;
            var plug = GetPlugin(securityContext, uniqueName, out var id);
            return (from p in securityContext.GenericPluginParams
                where p.WebPluginId == id
                select p).ToList();
        }
    }
}
