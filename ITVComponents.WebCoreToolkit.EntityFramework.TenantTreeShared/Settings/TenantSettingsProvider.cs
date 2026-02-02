using System;
using System.Linq;
using ITVComponents.WebCoreToolkit.Configuration;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Helpers.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantTreeShared.Helpers.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantTreeShared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantTreeShared.Models.TreeModels;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantTreeShared.Settings
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
        /// Holds the db-context with the tenant-settings
        /// </summary>
        private readonly IHierarchyTenantContext<TTenant, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig> dbContext;

        /// <summary>
        /// Initializes a new instance of the TenantSettinsgProvider class
        /// </summary>
        /// <param name="dbContext"></param>
        public TenantSettingsProvider(IHierarchyTenantContext<TTenant, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig> dbContext)
        {
            this.dbContext = dbContext;
        }

        /// <summary>
        /// Gets a Json-formatted setting with the given key
        /// </summary>
        /// <param name="key">the demanded key</param>
        /// <returns>the string-representation of the requested setting</returns>
        public string GetJsonSetting(string key)
        {
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
