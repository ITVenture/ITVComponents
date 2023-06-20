using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.EFRepo.Helpers;
using ITVComponents.EFRepo.Options;
using ITVComponents.Scripting.CScript.Helpers;
using ITVComponents.WebCoreToolkit.Configuration;
using ITVComponents.WebCoreToolkit.EntityFramework.DIIntegration;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.GlobalFiltering;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Logging;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Settings;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.WebPlugins;
using ITVComponents.WebCoreToolkit.Logging;
using ITVComponents.WebCoreToolkit.WebPlugins;
using Microsoft.EntityFrameworkCore;
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
            var contextArguments = typeof(TContext).GetSecurityContextArguments();
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
    }
}
