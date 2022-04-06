using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Configuration;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.Helpers;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.Navigation;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.Security;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Helpers;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.Logging;
using ITVComponents.WebCoreToolkit.Navigation;
using ITVComponents.WebCoreToolkit.Security;
using ITVComponents.WebCoreToolkit.WebPlugins;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.Extensions
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
                .RegisterExplicityInterfacesScoped<SecurityContext>()
                .AddScoped<ISecurityRepository, DbSecurityRepository<SecurityContext>>()
                .AddScoped<ITenantTemplateHelper, TenantTemplateHelper<SecurityContext>>();
        }

        /// <summary>
        /// Enables DbIdentities with the default SecurityContext db-context
        /// </summary>
        /// <typeparam name="TImpl">the implementation-type if derived from the base SecurityContext - class</typeparam>
        /// <param name="services">the services where the SecurityContext is injected</param>
        /// <param name="options">the options for the context</param>
        /// <returns>the serviceCollection instance that was passed as argument</returns>
        public static IServiceCollection UseDbIdentities<TImpl>(this IServiceCollection services, Action<DbContextOptionsBuilder> options) where TImpl:SecurityContext<TImpl>
        {
            return services.AddDbContext<TImpl>(options)
                .RegisterExplicityInterfacesScoped<TImpl>()
                .AddScoped<ISecurityRepository, DbSecurityRepository<TImpl>>()
                .AddScoped<ITenantTemplateHelper, TenantTemplateHelper<TImpl>>();
        }

        /// <summary>
        /// Enables DbIdentities with the default SecurityContext db-context
        /// </summary>
        /// <typeparam name="TImpl">the implementation-type if derived from the base SecurityContext - class</typeparam>
        /// <param name="services">the services where the SecurityContext is injected</param>
        /// <param name="options">the options for the context</param>
        /// <returns>the serviceCollection instance that was passed as argument</returns>
        public static IServiceCollection UseDbIdentities<TImpl, TTmpHelper>(this IServiceCollection services, Action<DbContextOptionsBuilder> options) 
            where TImpl : SecurityContext<TImpl>
            where TTmpHelper: TenantTemplateHelper<TImpl>
        {
            return services.AddDbContext<TImpl>(options)
                .RegisterExplicityInterfacesScoped<TImpl>()
                .AddScoped<ISecurityRepository, DbSecurityRepository<TImpl>>()
                .AddScoped<ITenantTemplateHelper, TTmpHelper>();
        }

        /// <summary>
        /// Activate DB-Navigation
        /// </summary>
        /// <param name="services">the Services-collection where to inject the DB-Navigation builder instance</param>
        /// <returns>the serviceCollection instance that was passed as argument</returns>
        public static IServiceCollection UseDbNavigation(this IServiceCollection services)
        {
            return services.AddScoped<INavigationBuilder, DbNavigationBuilder<SecurityContext>>();
        }

        /// <summary>
        /// Activate DB-Navigation
        /// </summary>
        /// <param name="services">the Services-collection where to inject the DB-Navigation builder instance</param>
        /// <returns>the serviceCollection instance that was passed as argument</returns>
        public static IServiceCollection UseDbNavigation<TImpl>(this IServiceCollection services) where TImpl:SecurityContext<TImpl>
        {
            return services.AddScoped<INavigationBuilder, DbNavigationBuilder<TImpl>>();
        }
    }
}
