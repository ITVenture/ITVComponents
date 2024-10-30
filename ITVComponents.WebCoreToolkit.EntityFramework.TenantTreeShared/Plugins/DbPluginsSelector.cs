using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.Base;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.FlatTenantModels;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.WebPlugins.Model;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.WebPlugins.Options;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantTreeShared.Models;
using ITVComponents.WebCoreToolkit.Models;
using ITVComponents.WebCoreToolkit.Models.Comparers;
using ITVComponents.WebCoreToolkit.Security;
using ITVComponents.WebCoreToolkit.WebPlugins;
using Microsoft.Extensions.Options;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantTreeShared.Plugins
{
    internal class DbPluginsSelector<TTenant, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation> :IWebPluginsSelector
    where TTenant : HierarchyTenant
    where TWebPlugin : WebPlugin<TTenant, TWebPlugin, TWebPluginGenericParameter>, new()
    where TWebPluginConstant : WebPluginConstant<TTenant>
    where TWebPluginGenericParameter : WebPluginGenericParameter<TTenant, TWebPlugin, TWebPluginGenericParameter>
    where TSequence : Sequence<TTenant>
    where TTenantSetting : TenantSetting<TTenant>
    where TTenantFeatureActivation : TenantFeatureActivation<TTenant>
    {
        private readonly IHierarchyTenantContext<TTenant, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation> securityContext;
        private readonly IPermissionScope scopeProvider;
        private readonly WebPluginBufferingOptions bufferConfig;

        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, DbPluginBufferInfo/*<TTenant, TWebPlugin, TWebPluginGenericParameter>*/>>
            bufferedPlugins = new ConcurrentDictionary<string, ConcurrentDictionary<string, DbPluginBufferInfo/*<TTenant, TWebPlugin, TWebPluginGenericParameter>*/>>();

        /// <summary>
        /// Initializes a new instance of hte DbPluginsSelector class
        /// </summary>
        /// <param name="securityContext">the injected security-db-context</param>
        public DbPluginsSelector(IHierarchyTenantContext<TTenant, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation> securityContext, IPermissionScope scopeProvider, IOptions<WebPluginBufferingOptions> bufferConfig)
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
            return from p in securityContext.WebPlugins
                where p.TenantId == null && !string.IsNullOrEmpty(p.StartupRegistrationConstructor)
                orderby p.UniqueName
                select p;


            /*if (securityContext.FilterAvailable && !securityContext.ShowAllTenants)
            {
                var tmp = 
                    (from rprot in (from t in securityContext.UpwardsTenantTreeView join p in securityContext.WebPlugins on t.ParentTenantId equals p.TenantId
                        select new {t.ParentTenantId, t.ParentLevel, p.UniqueName, p.WebPluginId} into gprot
                        group gprot by gprot.UniqueName into g1
                        select new {UniqueName=g1.Key, Level=g1.Min(hi => hi.ParentLevel), All=g1.ToArray()})
                    .Select(p => p.All.First(n => n.ParentLevel == p.Level))
                    join rp in securityContext.WebPlugins on rprot.WebPluginId equals rp.WebPluginId
                     select new WebPlugin{AutoLoad = rp.AutoLoad, Constructor = rp.Constructor, StartupRegistrationConstructor = rp.StartupRegistrationConstructor, UniqueName=rp.UniqueName})
                    .Union(from t in securityContext.WebPlugins where t.TenantId == null select new WebPlugin { AutoLoad = t.AutoLoad, Constructor = t.Constructor, StartupRegistrationConstructor = t.StartupRegistrationConstructor, UniqueName = t.UniqueName },
                        new WebPluginComparer())
                    .Where(n => !string.IsNullOrEmpty(n.StartupRegistrationConstructor));
            }

            if (string.IsNullOrEmpty(ExplicitPluginPermissionScope))
            {
                return from p in securityContext.WebPlugins
                    where p.TenantId == null && !string.IsNullOrEmpty(p.StartupRegistrationConstructor)
                    orderby p.UniqueName
                    select p;
            }

            return (from p in securityContext.WebPlugins
                join ht in securityContext.UpwardsTenantTreeView on p.TenantId equals ht.ParentTenantId
                select new { p.UniqueName, p.StartupRegistrationConstructor, ht.ParentLevel, ht.OutermostLeafTenantName, ht.OutermostLeafTenantId } into htg
                group htg by new {htg.UniqueName, htg.OutermostLeafTenantName} into gr
                let pl = gr.Min(m => m.ParentLevel)
                select new { UniqueName = gr.Key.UniqueName, TargetLevel = pl, StartupRegistrationConstrucotr = gr.First(n => n.ParentLevel == pl).StartupRegistrationConstructor, gr.Key.OutermostLeafTenantName } into s
                where !string.IsNullOrEmpty(s.StartupRegistrationConstrucotr) && s.OutermostLeafTenantName == ExplicitPluginPermissionScope
                orderby s.UniqueName
                select new WebPlugin { UniqueName = s.UniqueName, StartupRegistrationConstructor = s.StartupRegistrationConstrucotr })
                .Union(from p in securityContext.WebPlugins
                    where p.TenantId == null && !string.IsNullOrEmpty(p.StartupRegistrationConstructor)
                    orderby p.UniqueName
                    select p);*/
        }

        /// <summary>
        /// Gets the definition of a specific UniqueName
        /// </summary>
        /// <param name="uniqueName">the uniqueName for the desired plugin-definition</param>
        /// <returns>a WebPlugin definition that can be processed by the underlying factory</returns>
        public WebPlugin GetPlugin(string uniqueName)
        {
            return GetPlugin(uniqueName, out _);
        }

        private WebPlugin GetPlugin(string uniqueName, out int? webPluginId)
        {
            if (securityContext.FilterAvailable && !securityContext.ShowAllTenants && PluginBuffered(uniqueName, out var bufferInfo))
            {
                webPluginId = bufferInfo.WebPluginId;
                return bufferInfo.Plugin;
            }

            if (securityContext.FilterAvailable && !securityContext.ShowAllTenants)
            {
                var pi = (from p in securityContext.UpwardsTenantTreeView
                        join pin in securityContext.WebPlugins on p.ParentTenantId equals pin.TenantId
                        where pin.UniqueName == uniqueName
                        orderby p.ParentLevel
                        select pin).FirstOrDefault() ??
                    securityContext.WebPlugins.FirstOrDefault(n => n.TenantId == null && n.UniqueName == uniqueName);
                webPluginId = pi?.WebPluginId;
                return TryRegisterPlugin(uniqueName, pi);
            }

            if (string.IsNullOrEmpty(ExplicitPluginPermissionScope))
            {
                var pi =securityContext.WebPlugins.FirstOrDefault(n => n.TenantId == null && n.UniqueName == uniqueName);
                webPluginId = pi?.WebPluginId;
                return pi;
            }

            var pret = (from p in securityContext.UpwardsTenantTreeView
                         join pin in securityContext.WebPlugins on p.ParentTenantId equals pin.TenantId
                         where pin.UniqueName == uniqueName && p.OutermostLeafTenantName == ExplicitPluginPermissionScope
                         orderby p.ParentLevel
                         select pin).FirstOrDefault() ??
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
                Plugin = pluginData!=null?new WebPlugin
                {
                    AutoLoad = pluginData.AutoLoad,
                    Constructor = pluginData.Constructor,
                    StartupRegistrationConstructor = pluginData.StartupRegistrationConstructor,
                    UniqueName = pluginData.UniqueName  
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
            if (securityContext.FilterAvailable && !securityContext.ShowAllTenants)
            {
                return 
                    (from rprot in (from t in securityContext.UpwardsTenantTreeView
                                join p in securityContext.WebPlugins on t.ParentTenantId equals p.TenantId
                                select new { t.ParentTenantId, t.ParentLevel, p.UniqueName, p.WebPluginId } into gprot
                                group gprot by gprot.UniqueName into g1
                                select new { UniqueName = g1.Key, Level = g1.Min(hi => hi.ParentLevel), All = g1.ToArray() })
                            .Select(p => p.All.First(n => n.ParentLevel == p.Level))
                        join rp in securityContext.WebPlugins on rprot.WebPluginId equals rp.WebPluginId
                        select new WebPlugin { AutoLoad = rp.AutoLoad, Constructor = rp.Constructor, StartupRegistrationConstructor = rp.StartupRegistrationConstructor, UniqueName = rp.UniqueName })
                    .AsEnumerable()
                    .Union((from t in securityContext.WebPlugins where t.TenantId == null select new WebPlugin { AutoLoad = t.AutoLoad, Constructor = t.Constructor, StartupRegistrationConstructor = t.StartupRegistrationConstructor, UniqueName = t.UniqueName }).AsEnumerable(),
                        new WebPluginComparer())
                    .Where(n => !string.IsNullOrEmpty(n.Constructor) && n.AutoLoad);
                /*return from p in securityContext.WebPlugins
                    where !string.IsNullOrEmpty(p.Constructor) && p.AutoLoad
                       orderby p.UniqueName
                    select p;*/
            }

            if (string.IsNullOrEmpty(ExplicitPluginPermissionScope))
            {
                return from p in securityContext.WebPlugins
                    where p.TenantId == null && !string.IsNullOrEmpty(p.Constructor) && p.AutoLoad
                       orderby p.UniqueName
                    select p;
            }

            return
                (from rprot in (from t in securityContext.UpwardsTenantTreeView.Where(n => n.OutermostLeafTenantName == ExplicitPluginPermissionScope)
                            join p in securityContext.WebPlugins on t.ParentTenantId equals p.TenantId
                            select new { t.ParentTenantId, t.ParentLevel, p.UniqueName, p.WebPluginId } into gprot
                            group gprot by gprot.UniqueName into g1
                            select new { UniqueName = g1.Key, Level = g1.Min(hi => hi.ParentLevel), All = g1.ToArray() })
                        .Select(p => p.All.First(n => n.ParentLevel == p.Level))
                    join rp in securityContext.WebPlugins on rprot.WebPluginId equals rp.WebPluginId
                    select new WebPlugin { AutoLoad = rp.AutoLoad, Constructor = rp.Constructor, StartupRegistrationConstructor = rp.StartupRegistrationConstructor, UniqueName = rp.UniqueName })
                .AsEnumerable()
                .Union((from t in securityContext.WebPlugins where t.TenantId == null select new WebPlugin { AutoLoad = t.AutoLoad, Constructor = t.Constructor, StartupRegistrationConstructor = t.StartupRegistrationConstructor, UniqueName = t.UniqueName }).AsEnumerable(),
                    new WebPluginComparer())
                .Where(n => !string.IsNullOrEmpty(n.Constructor) && n.AutoLoad);
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
