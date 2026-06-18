using System.Linq;
using ITVComponents.WebCoreToolkit.Configuration;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Settings
{
    internal class GlobalSettingsProvider : IGlobalSettingsProvider
    {
        /// <summary>
        /// Per-operation factory for the system context (Blazor-safe: a fresh, short-lived context per call instead
        /// of a shared circuit-scoped one).
        /// </summary>
        private readonly IToolkitContextFactory contextFactory;

        /// <summary>
        /// Initializes a new instance of the TenantSettinsgProvider class
        /// </summary>
        /// <param name="contextFactory">factory yielding a fresh per-operation system context</param>
        public GlobalSettingsProvider(IToolkitContextFactory contextFactory)
        {
            this.contextFactory = contextFactory;
        }

        /// <summary>
        /// Gets a Json-formatted setting with the given key
        /// </summary>
        /// <param name="key">the demanded key</param>
        /// <returns>the string-representation of the requested setting</returns>
        public string GetJsonSetting(string key)
        {
            using var lease = contextFactory.Lease<ICoreSystemContext>();
            return lease.Context.GlobalSettings.FirstOrDefault(n => n.SettingsKey == key && n.JsonSetting)?.SettingsValue;
        }

        /// <summary>
        /// Gets an unformatted plain setting with the given key
        /// </summary>
        /// <param name="key">the demanded key</param>
        /// <returns>the string representation of the requested setting</returns>
        public string GetLiteralSetting(string key)
        {
            using var lease = contextFactory.Lease<ICoreSystemContext>();
            return lease.Context.GlobalSettings.FirstOrDefault(n => n.SettingsKey == key && !n.JsonSetting)?.SettingsValue;
        }
    }
}
