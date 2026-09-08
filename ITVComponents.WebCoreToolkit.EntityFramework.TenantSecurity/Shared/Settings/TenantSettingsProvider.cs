using System;
using System.Linq;
using ITVComponents.Helpers;
using ITVComponents.Logging;
using ITVComponents.WebCoreToolkit.Configuration;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.DependencyInjection;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Settings
{
    /// <summary>
    /// Tenant-capable settings-provider
    /// </summary>
    internal class TenantSettingsProvider<TTenant, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig> :IScopedSettingsProvider
    where TTenant: Tenant 
    where TWebPlugin : WebPlugin<TTenant, TWebPlugin, TWebPluginGenericParameter>
    where TWebPluginConstant : WebPluginConstant<TTenant>
    where TWebPluginGenericParameter : WebPluginGenericParameter<TTenant, TWebPlugin, TWebPluginGenericParameter>
    where TSequence : Sequence<TTenant>
    where TTenantSetting : TenantSetting<TTenant>, new()
    where TTenantFeatureActivation : TenantFeatureActivation<TTenant>
    where TTrustConfig : BaseTenantContextSecurityTrustConfig<TTrustConfig>, new()
    where TExternalOAuthService : ExternalOAuthService<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
    where TExternalOAuthServiceState : ExternalOAuthServiceState<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
    where TExternalOAuthServiceTenantLogin : ExternalOAuthServiceTenantLogin<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
    {
        /// <summary>
        /// Per-operation factory for the tenant-settings context (Blazor-safe: a fresh, short-lived context per call
        /// instead of a shared circuit-scoped one).
        /// </summary>
        private readonly IToolkitContextFactory contextFactory;

        /// <summary>
        /// Records why a lookup came back with nothing. Built under a fixed category rather than injected as
        /// <c>ILogger&lt;TenantSettingsProvider&lt;…&gt;&gt;</c>: eleven type arguments make a category name no
        /// log-level filter can be written against.
        /// </summary>
        private readonly ILogger logger;

        /// <summary>
        /// Initializes a new instance of the TenantSettinsgProvider class
        /// </summary>
        /// <param name="contextFactory">factory yielding a fresh per-operation tenant-settings context</param>
        /// <param name="loggerFactory">factory for the logger that explains an empty lookup</param>
        public TenantSettingsProvider(IToolkitContextFactory contextFactory, ILoggerFactory loggerFactory)
        {
            this.contextFactory = contextFactory;
            logger = loggerFactory?.CreateLogger(
                "ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Settings.TenantSettingsProvider");
        }

        /// <summary>
        /// Leases a fresh per-operation tenant-settings context for the duration of a single operation.
        /// </summary>
        private IContextLease<IBaseTenantContext<TTenant, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig>> LeaseDb()
            => contextFactory.Lease<IBaseTenantContext<TTenant, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig>>();

        /// <summary>
        /// Gets a Json-formatted setting with the given key
        /// </summary>
        /// <param name="key">the demanded key</param>
        /// <returns>the string-representation of the requested setting</returns>
        public string GetJsonSetting(string key)
        {
            using var lease = LeaseDb();
            var dbContext = lease.Context;
            var retVal = dbContext.TenantSettings.FirstOrDefault(n => n.SettingsKey == key && n.JsonSetting)?.SettingsValue;
            if (retVal == null)
            {
                DescribeMiss(dbContext, key, true);
            }

            return retVal;
        }

        /// <summary>
        /// Writes down WHY a settings lookup came back with nothing: whether rows for the key exist at all, and
        /// whether they are visible to this context. From the outside "no such setting" and "the row is there but
        /// this context cannot see it" are the same silence, and a caller reports both as "not configured".
        /// </summary>
        /// <remarks>
        /// Only on the miss path, and only when debug logging is on - the counting queries are not free. The
        /// filtered/unfiltered pair is the point: a row that appears only unfiltered exists and is being filtered
        /// away, which is a different problem from a row that is not there.
        /// </remarks>
        private void DescribeMiss(IBaseTenantContext<TTenant, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig> dbContext, string key, bool jsonSetting)
        {
            if (logger?.IsEnabled(LogLevel.Debug) != true)
            {
                return;
            }

            try
            {
                var keyRows = dbContext.TenantSettings.Count(n => n.SettingsKey == key && n.JsonSetting == jsonSetting);
                var keyRowsUnfiltered = dbContext.TenantSettings.IgnoreQueryFilters()
                    .Count(n => n.SettingsKey == key && n.JsonSetting == jsonSetting);
                var matches = string.Join("; ", dbContext.TenantSettings.IgnoreQueryFilters()
                    .Where(n => n.SettingsKey == key)
                    .Select(n => new { n.TenantId, n.JsonSetting })
                    .Take(10).ToList()
                    .Select(n => $"TenantId={n.TenantId},Json={n.JsonSetting}"));

                logger.LogDebug(
                    "Tenant-setting '{SettingsKey}' (JsonSetting={JsonSetting}) resolved to nothing on the FLAT provider. " +
                    "CurrentTenantId={CurrentTenantId}; matching TenantSettings rows={KeyRows} (unfiltered {KeyRowsUnfiltered}); " +
                    "rows for that key: [{Matches}]",
                    key, jsonSetting, dbContext.CurrentTenantId?.ToString() ?? "<null>",
                    keyRows, keyRowsUnfiltered, string.IsNullOrEmpty(matches) ? "none" : matches);
            }
            catch (Exception ex)
            {
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
            return dbContext.TenantSettings.FirstOrDefault(n => n.SettingsKey == key && !n.JsonSetting)?.SettingsValue;
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
                return dbContext.TenantSettings.FirstOrDefault(n =>
                    n.SettingsKey == key && n.JsonSetting && n.Tenant.TenantName == explicitUserScope)?.SettingsValue;
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
                return dbContext.TenantSettings.FirstOrDefault(n =>
                    n.SettingsKey == key && !n.JsonSetting && n.Tenant.TenantName == explicitUserScope)?.SettingsValue;
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
