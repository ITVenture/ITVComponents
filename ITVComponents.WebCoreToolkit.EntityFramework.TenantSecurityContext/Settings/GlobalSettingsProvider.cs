using System.Linq;
using ITVComponents.WebCoreToolkit.Configuration;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.Settings
{
    internal class GlobalSettingsProvider : IGlobalSettingsProvider
    {
        /// <summary>
        /// Holds the db-context with the tenant-settings
        /// </summary>
        private readonly SecurityContext dbContext;

        /// <summary>
        /// Initializes a new instance of the TenantSettinsgProvider class
        /// </summary>
        /// <param name="dbContext"></param>
        public GlobalSettingsProvider(SecurityContext dbContext)
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
            return dbContext.GlobalSettings.FirstOrDefault(n => n.SettingsKey == key && n.JsonSetting)?.SettingsValue;
        }

        /// <summary>
        /// Gets an unformatted plain setting with the given key
        /// </summary>
        /// <param name="key">the demanded key</param>
        /// <returns>the string representation of the requested setting</returns>
        public string GetLiteralSetting(string key)
        {
            return dbContext.GlobalSettings.FirstOrDefault(n => n.SettingsKey == key && !n.JsonSetting)?.SettingsValue;
        }
    }
}
