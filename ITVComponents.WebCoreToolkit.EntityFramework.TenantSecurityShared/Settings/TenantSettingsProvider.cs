using System.Linq;
using ITVComponents.WebCoreToolkit.Configuration;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Settings
{
    /// <summary>
    /// Tenant-capable settings-provider
    /// </summary>
    internal class TenantSettingsProvider:IScopedSettingsProvider
    {
        /// <summary>
        /// Holds the db-context with the tenant-settings
        /// </summary>
        private readonly IBaseTenantContext dbContext;

        /// <summary>
        /// Initializes a new instance of the TenantSettinsgProvider class
        /// </summary>
        /// <param name="dbContext"></param>
        public TenantSettingsProvider(IBaseTenantContext dbContext)
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
            return dbContext.TenantSettings.FirstOrDefault(n => n.SettingsKey == key && n.JsonSetting)?.SettingsValue;
        }

        /// <summary>
        /// Gets an unformatted plain setting with the given key
        /// </summary>
        /// <param name="key">the demanded key</param>
        /// <returns>the string representation of the requested setting</returns>
        public string GetLiteralSetting(string key)
        {
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
                return dbContext.TenantSettings.FirstOrDefault(n =>
                    n.SettingsKey == key && !n.JsonSetting && n.Tenant.TenantName == explicitUserScope)?.SettingsValue;
            }

            return GetLiteralSetting(key);
        }
    }
}
