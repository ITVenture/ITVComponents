using ITVComponents.EFRepo.Helpers;
using ITVComponents.EFRepo.Options;
using ITVComponents.Helpers;
using ITVComponents.Scripting.CScript.Helpers;
using ITVComponents.WebCoreToolkit.Configuration;
using ITVComponents.WebCoreToolkit.Cookies;
using ITVComponents.WebCoreToolkit.EntityFramework.DIIntegration;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Cookies;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Logging;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Options;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.WebPlugins.Options;
//using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Extensions;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.GlobalFiltering;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Settings;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.WebPlugins;
using ITVComponents.WebCoreToolkit.Logging;
using ITVComponents.WebCoreToolkit.Security.ComponentTrust;
using ITVComponents.WebCoreToolkit.WebPlugins;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Security.ComponentTrust;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Extensions
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
            return Shared.Extensions.DependencyExtensions.UseDbLogAdapter(services);
        }

        /// <summary>
        /// Activates DB-Plugins
        /// </summary>
        /// <param name="services">the services-collection where to inject the DB-Plugin Selector instance</param>
        /// <returns>the ServicesCollection instance that was passed as argument</returns>
        public static IServiceCollection UseDbPlugins<TContext>(this IServiceCollection services, int bufferDuration) where TContext : DbContext
        {
            var tff = typeof(TContext).FinalizeType(typeof(DbPluginsSelector<,,,,,,,,,,>));
            services.Configure<WebPluginBufferingOptions>(n => n.BufferDuration = bufferDuration);
            return services.AddScoped(typeof(IWebPluginsSelector), tff);
        }

        /// <summary>
        /// Activates Tenant-driven-Settings
        /// </summary>
        /// <param name="services">the Services-collection where to inject the DB-Navigation builder instance</param>
        /// <returns>the serviceCollection instance that was passed as argument</returns>
        public static IServiceCollection UseTenantSettings<TContext>(this IServiceCollection services) where TContext : DbContext
        {
            var tff = typeof(TContext).FinalizeType(typeof(TenantSettingsProvider<,,,,,,,,,,>));
            return services.AddScoped(typeof(IScopedSettingsProvider), tff);
        }

        /// <summary>
        /// Activates Db-Driven Globalsettings
        /// </summary>
        /// <param name="services">the Services-collection where to inject the DB-Navigation builder instance</param>
        /// <returns>the serviceCollection instance that was passed as argument</returns>
        public static IServiceCollection UseDbGlobalSettings(this IServiceCollection services)
        {
            return Shared.Extensions.DependencyExtensions.UseDbGlobalSettings(services);
        }

        /// <summary>
        /// Configures methods for the used SecurityContext that must be implemented db-specific
        /// </summary>
        /// <param name="services">the services where the Configuration is injected into</param>
        /// <param name="contextType">the context-type to configure</param>
        /// <param name="options">a callback identifying the options that need to be injected</param>
        /// <returns>the serviceCollection instance that was passed as argument</returns>
        public static IServiceCollection ConfigureMethods(this IServiceCollection services, Type contextType,
            Action<IContextModelBuilderOptions> options)
        {
            var method = LambdaHelper.GetMethodInfo(() => ConfigureMethods<DbContext>(services, options))
                .GetGenericMethodDefinition();
            method = method.MakeGenericMethod(contextType);
            return (IServiceCollection)method.Invoke(null, new object[] { services, options });
        }


        /// <summary>
        /// Configures methods for the used SecurityContext that must be implemented db-specific
        /// </summary>
        /// <typeparam name="TContext">the context-type to configure</typeparam>
        /// <param name="services">the services where the Configuration is injected into</param>
        /// <param name="options">a callback identifying the options that need to be injected</param>
        /// <returns>the serviceCollection instance that was passed as argument</returns>
        public static IServiceCollection ConfigureMethods<TContext>(this IServiceCollection services, Action<IContextModelBuilderOptions> options)
        {
            return services.Configure<DbContextModelBuilderOptions<TContext>>(o =>
            {
                options(o);
            });
        }


        public static IServiceCollection ConfigureGlobalFilters<TContext>(this IServiceCollection services)
        {
            var contextArguments = typeof(TContext).GetInterfaceGenericArgumentsOf(fixTypeEntries:
                [("TImpl", typeof(TContext)), ("TContext", typeof(TContext))]);
            var mth = GlobalFilterBuilder.GetConfigureMethod(contextArguments);
            mth.Invoke(null, new[] { services });
            return services;
        }

        public static IServiceCollection ConfigureDefaultContextUserProvider<TContext>(this IServiceCollection services) 
            where TContext : DbContext, IUserAwareContext
        {
            services.AddScoped<ICurrentUserProvider<TContext>, UserAwareContextUserProvider<TContext>>();
            return services;
        }

        public static IServiceCollection ConfigureGlobalFilters(this IServiceCollection services, Type contextType)
        {
            var method = LambdaHelper.GetMethodInfo(() => ConfigureGlobalFilters<DbContext>(services))
                .GetGenericMethodDefinition();
            method = method.MakeGenericMethod(contextType);
            return (IServiceCollection)method.Invoke(null, new object[] { services });
        }

        public static IServiceCollection ConfigureDefaultContextUserProvider(this IServiceCollection services,
            Type contextType)
        {
            var method = LambdaHelper.GetMethodInfo(() => ConfigureDefaultContextUserProvider<DummyUserAwareDbContext>(services))
                .GetGenericMethodDefinition();
            method = method.MakeGenericMethod(contextType);
            return (IServiceCollection)method.Invoke(null, new object[] { services });
        }

        public static IServiceCollection UseServerCookies(this IServiceCollection services,
            Action<ServerCookieOptions> configure)
        {
            services.AddScoped<ICookieService, DbCookieService>();
            if (configure != null)
            {
                services.Configure<ServerCookieOptions>(configure);
            }

            return services;
        }

        public static IServiceCollection UseDefaultSecurityAccessProvider(this IServiceCollection services)
        {
            return services.AddScoped<ISecurityAccessProvider, DbSecurityAccessProvider>();
        }
    }
}
