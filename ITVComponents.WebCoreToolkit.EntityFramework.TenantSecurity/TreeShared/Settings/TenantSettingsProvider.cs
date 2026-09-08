using System;
using System.Linq;
using ITVComponents.Helpers;
using ITVComponents.Logging;
using ITVComponents.WebCoreToolkit.Configuration;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.DependencyInjection;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Helpers.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Models.TreeModels;
using Microsoft.EntityFrameworkCore;

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
            var retVal = (from t in dbContext.UpwardsTenantTreeView
                join s in dbContext.TenantSettings on t.ParentTenantId equals s.TenantId
                where s.SettingsKey == key && s.JsonSetting && (t.ParentLevel == 1 || s.Inheritable)
                orderby t.ParentLevel
                select s).FirstOrDefault()?.SettingsValue;
            if (retVal == null)
            {
                DescribeMiss(dbContext, key, true);
            }

            return retVal;
        }

        /// <summary>
        /// Writes down WHY a settings lookup came back with nothing. The join has three inputs that can each be
        /// empty on their own - the upwards tree, the settings rows for the key, and the current tenant the tree
        /// is walked from - and from the outside all three produce the same silence, which a caller then reports
        /// as "the setting is not configured".
        /// </summary>
        /// <remarks>
        /// Deliberately only on the miss path: the counting queries are not free, and on the hit path there is
        /// nothing to explain. Both counts are taken twice, once as the caller sees them and once with
        /// <c>IgnoreQueryFilters</c>. That pair is the point of the whole method - if the row appears only in
        /// the unfiltered count, the setting exists and something is filtering it away, which is a different
        /// problem entirely from a row that is not there.
        /// </remarks>
        private void DescribeMiss(IHierarchyTenantContext<TTenant, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig> dbContext, string key, bool jsonSetting)
        {
            try
            {
                var treeRows = dbContext.UpwardsTenantTreeView.Count();
                var treeRowsUnfiltered = dbContext.UpwardsTenantTreeView.IgnoreQueryFilters().Count();
                var keyRows = dbContext.TenantSettings.Count(n => n.SettingsKey == key && n.JsonSetting == jsonSetting);
                var keyRowsUnfiltered = dbContext.TenantSettings.IgnoreQueryFilters()
                    .Count(n => n.SettingsKey == key && n.JsonSetting == jsonSetting);
                var matches = string.Join("; ", dbContext.TenantSettings.IgnoreQueryFilters()
                    .Where(n => n.SettingsKey == key)
                    .Select(n => new { n.TenantId, n.JsonSetting, n.Inheritable })
                    .Take(10).ToList()
                    .Select(n => $"TenantId={n.TenantId},Json={n.JsonSetting},Inheritable={n.Inheritable}"));

                LogEnvironment.LogDebugEvent(
                    $"Tenant-setting '{key}' (JsonSetting={jsonSetting}) resolved to nothing. CurrentTenantId={dbContext.CurrentTenantId?.ToString() ?? "<null>"}; " +
                    $"UpwardsTenantTree rows={treeRows} (unfiltered {treeRowsUnfiltered}); matching TenantSettings rows={keyRows} (unfiltered {keyRowsUnfiltered}); " +
                    $"rows for that key: [{(string.IsNullOrEmpty(matches) ? "none" : matches)}]",
                    LogSeverity.Report);
            }
            catch (Exception ex)
            {
                // The explanation must never be worse than the thing it explains: a caller asking for an
                // optional setting is not going to be failed because the diagnosis could not be produced.
                LogEnvironment.LogEvent(
                    $"Unable to diagnose why tenant-setting '{key}' resolved to nothing: {ex.OutlineException()}",
                    LogSeverity.Warning);
            }
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
            var retVal = (from t in dbContext.UpwardsTenantTreeView
                join s in dbContext.TenantSettings on t.ParentTenantId equals s.TenantId
                where s.SettingsKey == key && !s.JsonSetting && (t.ParentLevel == 1 || s.Inheritable)
                    orderby t.ParentLevel
                select s).FirstOrDefault()?.SettingsValue;
            if (retVal == null)
            {
                DescribeMiss(dbContext, key, false);
            }

            return retVal;
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
