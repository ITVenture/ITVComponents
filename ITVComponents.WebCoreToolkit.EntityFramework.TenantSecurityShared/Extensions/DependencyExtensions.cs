using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Configuration;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Logging;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Settings;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.WebPlugins;
using ITVComponents.WebCoreToolkit.Logging;
using ITVComponents.WebCoreToolkit.WebPlugins;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Extensions
{
    public static class DependencyExtensions
    {
        /// <summary>
        /// Enables the Db Log-Adapter
        /// </summary>
        /// <param name="services">the Services-collection where to inject the DB-Navigation builder instance</param>
        /// <returns>the serviceCollection instance that was passed as argument</returns>
        public static IServiceCollection UseDbLogAdapter(this IServiceCollection services)
        {
            return services.AddScoped<ILogOutputAdapter, DbLogOutputAdapter>();
        }

        /// <summary>
        /// Activates DB-Plugins
        /// </summary>
        /// <param name="services">the services-collection where to inject the DB-Plugin Selector instance</param>
        /// <returns>the ServicesCollection instance that was passed as argument</returns>
        public static IServiceCollection UseDbPlugins(this IServiceCollection services)
        {
            return services.AddScoped<IWebPluginsSelector, DbPluginsSelector>();
        }

        /// <summary>
        /// Activates Tenant-driven-Settings
        /// </summary>
        /// <param name="services">the Services-collection where to inject the DB-Navigation builder instance</param>
        /// <returns>the serviceCollection instance that was passed as argument</returns>
        public static IServiceCollection UseTenantSettings(this IServiceCollection services)
        {
            return services.AddScoped<IScopedSettingsProvider, TenantSettingsProvider>();
        }

        /// <summary>
        /// Activates Db-Driven Globalsettings
        /// </summary>
        /// <param name="services">the Services-collection where to inject the DB-Navigation builder instance</param>
        /// <returns>the serviceCollection instance that was passed as argument</returns>
        public static IServiceCollection UseDbGlobalSettings(this IServiceCollection services)
        {
            return services.AddScoped<IGlobalSettingsProvider, GlobalSettingsProvider>();
        }
    }
}
