using System;
using System.Linq;
using ITVComponents.WebCoreToolkit.Configuration;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.DependencyInjection;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Helpers.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Models.TreeModels;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Settings
{
    /// <summary>
    /// Tenant-capable settings-provider
    /// </summary>
    internal class TenantSettingsProvider<TTenant, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig> :IScopedSettingsProvider
    where TTenant: HierarchyTenant 
    where TWebPlugin : HierarchyWebPlugin<TTenant, TWebPlugin, TWebPluginGenericParameter>
    where TWebPluginConstant : HierarchyWebPluginConstant<TTenant>
    where TWebPluginGenericParameter : HierarchyWebPluginGenericParameter<TTenant, TWebPlugin, TWebPluginGenericParameter>
    where TSequence : Sequence<TTenant>
    where TTenantSetting : HierarchyTenantSetting<TTenant>, new()
    where TTenantFeatureActivation : TenantFeatureActivation<TTenant>
    where TTrustConfig : HierarchyTenantContextSecurityTrustConfig<TTrustConfig>, new()
    where TExternalOAuthService : HierarchyExternalOAuthService<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
    where TExternalOAuthServiceState : HierarchyExternalOAuthServiceState<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
    where TExternalOAuthServiceTenantLogin : HierarchyExternalOAuthServiceTenantLogin<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
    {
        /// <summary>
        /// Per-operation factory for the tenant-settings context (Blazor-safe: a fresh, short-lived context per call
        /// instead of a shared circuit-scoped one).
        /// </summary>
        private readonly IToolkitContextFactory contextFactory;

        /// <summary>
        /// Initializes a new instance of the TenantSettinsgProvider class
        /// </summary>
        /// <param name="contextFactory">factory yielding a fresh per-operation tenant-settings context</param>
        public TenantSettingsProvider(IToolkitContextFactory contextFactory)
        {
            this.contextFactory = contextFactory;
        }

        /// <summary>
        /// Leases a fresh per-operation tenant-settings context for the duration of a single operation.
        /// </summary>
        private IContextLease<IHierarchyTenantContext<TTenant, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig>> LeaseDb()
            => contextFactory.Lease<IHierarchyTenantContext<TTenant, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig>>();

        /// <summary>
        /// Gets a Json-formatted setting with the given key
        /// </summary>
        /// <param name="key">the demanded key</param>
        /// <returns>the string-representation of the requested setting</returns>
        public string GetJsonSetting(string key)
        {
            using var lease = LeaseDb();
            var dbContext = lease.Context;
            return (from t in dbContext.UpwardsTenantTreeView
                join s in dbContext.TenantSettings on t.ParentTenantId equals s.TenantId
                where s.SettingsKey == key && s.JsonSetting && (t.ParentLevel == 1 || s.Inheritable)
                orderby t.ParentLevel
                select s).FirstOrDefault()?.SettingsValue;
        }

        /// <summary>
        /// Gets an unformatted plain setting with the given key
        /// </summary>
        /// <param name="key">the demanded key</param>
        /// <returns>the string representation of the requested setting</returns>
        public string GetLiteralSetting(string key)
        {
            using var lease = LeaseDb();
            var dbContext = lease.Context;
            return (from t in dbContext.UpwardsTenantTreeView
                join s in dbContext.TenantSettings on t.ParentTenantId equals s.TenantId
                where s.SettingsKey == key && !s.JsonSetting && (t.ParentLevel == 1 || s.Inheritable)
                    orderby t.ParentLevel
                select s).FirstOrDefault()?.SettingsValue;
        }

        /// <summary>
        /// Gets a Json-formatted setting with the given key
        /// </summary>
        /// <param name="key">the demanded key</param>
        /// <param name="explicitUserScope">the explicit scope under which to get the requested settings</param>
        /// <returns>the string-representation of the requested setting</returns>
        public string GetJsonSetting(string key, string explicitUserScope)
        {
            if (!string.IsNullOrEmpty(explicitUserScope))
            {
                using var lease = LeaseDb();
                var dbContext = lease.Context;
                return (from t in dbContext.UpwardsTenantTreeView.Where(n => n.OutermostLeafTenantName == explicitUserScope)
                    join s in dbContext.TenantSettings on t.ParentTenantId equals s.TenantId
                    where s.SettingsKey == key && s.JsonSetting && (t.ParentLevel == 1 || s.Inheritable)
                    orderby t.ParentLevel
                    select s).FirstOrDefault()?.SettingsValue;
            }

            return GetJsonSetting(key);
        }

        /// <summary>
        /// Gets an unformatted plain setting with the given key
        /// </summary>
        /// <param name="key">the demanded key</param>
        /// <param name="explicitUserScope">the explicit scope under which to get the requested settings</param>
        /// <returns>the string representation of the requested setting</returns>
        public string GetLiteralSetting(string key, string explicitUserScope)
        {
            if (!string.IsNullOrEmpty(explicitUserScope))
            {
                using var lease = LeaseDb();
                var dbContext = lease.Context;
                return (from t in dbContext.UpwardsTenantTreeView.Where(n => n.OutermostLeafTenantName == explicitUserScope)
                    join s in dbContext.TenantSettings on t.ParentTenantId equals s.TenantId
                    where s.SettingsKey == key && !s.JsonSetting && (t.ParentLevel == 1 || s.Inheritable)
                        orderby t.ParentLevel
                    select s).FirstOrDefault()?.SettingsValue;
            }

            return GetLiteralSetting(key);
        }

        public void UpdateJsonSetting(string key, string explicitUserScope, string value)
        {
            using var lease = LeaseDb();
            var dbContext = lease.Context;
            var tenantId = dbContext.CurrentTenantId ?? 0;
            if (!string.IsNullOrEmpty(explicitUserScope))
            {
                tenantId = dbContext.Tenants.First(n => n.TenantName == explicitUserScope).TenantId;
            }

            if (tenantId == 0)
            {
                throw new InvalidOperationException("A valid tenant is required for this operation!");
            }

            var original = dbContext.TenantSettings.FirstOrDefault(n =>
                n.SettingsKey == key && n.JsonSetting && n.TenantId == tenantId);
            if (original == null)
            {
                original = new TTenantSetting()
                {
                    JsonSetting = true,
                    SettingsKey = key,
                    TenantId = tenantId
                };
                dbContext.TenantSettings.Add(original);
            }

            original.SettingsValue = value;
            dbContext.SaveChanges();
        }

        public void UpdateLiteralSetting(string key, string explicitUserScope, string value)
        {
            using var lease = LeaseDb();
            var dbContext = lease.Context;
            var tenantId = dbContext.CurrentTenantId ?? 0;
            if (!string.IsNullOrEmpty(explicitUserScope))
            {
                tenantId = dbContext.Tenants.First(n => n.TenantName == explicitUserScope).TenantId;
            }

            if (tenantId == 0)
            {
                throw new InvalidOperationException("A valid tenant is required for this operation!");
            }

            var original = dbContext.TenantSettings.FirstOrDefault(n =>
                n.SettingsKey == key && !n.JsonSetting && n.TenantId == tenantId);
            if (original == null)
            {
                original = new TTenantSetting()
                {
                    JsonSetting = false,
                    SettingsKey = key,
                    TenantId = tenantId
                };
                dbContext.TenantSettings.Add(original);
            }

            original.SettingsValue = value;
            dbContext.SaveChanges();
        }
    }
}
