using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Configuration;
using ITVComponents.WebCoreToolkit.EntityFramework.Logging;
using ITVComponents.WebCoreToolkit.EntityFramework.Navigation;
using ITVComponents.WebCoreToolkit.EntityFramework.Options.Diagnostics;
using ITVComponents.WebCoreToolkit.EntityFramework.Options.ForeignKeys;
using ITVComponents.WebCoreToolkit.EntityFramework.Security;
using ITVComponents.WebCoreToolkit.EntityFramework.Settings;
using ITVComponents.WebCoreToolkit.EntityFramework.WebPlugins;
using ITVComponents.WebCoreToolkit.Logging;
using ITVComponents.WebCoreToolkit.Navigation;
using ITVComponents.WebCoreToolkit.Security;
using ITVComponents.WebCoreToolkit.WebPlugins;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Extensions
{
    public static class DependencyExtensions
    {
        /// <summary>
        /// Enables DbIdentities with the default SecurityContext db-context
        /// </summary>
        /// <param name="services">the services where the SecurityContext is injected</param>
        /// <param name="options">the options for the context</param>
        /// <returns>the serviceCollection instance that was passed as argument</returns>
        public static IServiceCollection UseDbIdentities(this IServiceCollection services, Action<DbContextOptionsBuilder> options)
        {
            return services.AddDbContext<SecurityContext>(options)
                .AddScoped<ISecurityRepository, DbSecurityRepository>();
        }

        /// <summary>
        /// Enables DbIdentities with the default SecurityContext db-context
        /// </summary>
        /// <typeparam name="TImpl">the implementation-type if derived from the base SecurityContext - class</typeparam>
        /// <param name="services">the services where the SecurityContext is injected</param>
        /// <param name="options">the options for the context</param>
        /// <returns>the serviceCollection instance that was passed as argument</returns>
        public static IServiceCollection UseDbIdentities<TImpl>(this IServiceCollection services, Action<DbContextOptionsBuilder> options) where TImpl:SecurityContext
        {
            return services.AddDbContext<TImpl>(options)
                .AddDbContext<SecurityContext, TImpl>(options)
                .AddScoped<ISecurityRepository, DbSecurityRepository>();
        }

        /// <summary>
        /// Configures the Foreignkey - EndPoint
        /// </summary>
        /// <param name="services">the services-collection in which the options are injected</param>
        /// <param name="options">a callback for options</param>
        /// <returns>the provided ServiceCollection for method-chaining</returns>
        public static IServiceCollection ConfigureForeignKeys(this IServiceCollection services, Action<ForeignKeySourceOptions> options)
        {
            return services.Configure(options);
        }

        /// <summary>
        /// Configures the Diagnostics - EndPoint
        /// </summary>
        /// <param name="services">the services-collection in which the options are injected</param>
        /// <param name="options">a callback for options</param>
        /// <returns>the provided ServiceCollection for method-chaining</returns>
        public static IServiceCollection ConfigureDiagnosticsQueries(this IServiceCollection services, Action<DiagnosticsSourceOptions> options)
        {
            return services.Configure(options);
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
        /// Activate DB-Navigation
        /// </summary>
        /// <param name="services">the Services-collection where to inject the DB-Navigation builder instance</param>
        /// <returns>the serviceCollection instance that was passed as argument</returns>
        public static IServiceCollection UseDbNavigation(this IServiceCollection services)
        {
            return services.AddScoped<INavigationBuilder, DbNavigationBuilder>();
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

        /// <summary>
        /// Enables the Db Log-Adapter
        /// </summary>
        /// <param name="services">the Services-collection where to inject the DB-Navigation builder instance</param>
        /// <returns>the serviceCollection instance that was passed as argument</returns>
        public static IServiceCollection UseDbLogAdapter(this IServiceCollection services)
        {
            return services.AddScoped<ILogOutputAdapter, DbLogOutputAdapter>();
        }
    }
}
