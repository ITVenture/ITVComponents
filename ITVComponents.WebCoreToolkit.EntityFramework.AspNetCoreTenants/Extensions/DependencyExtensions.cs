using System;
using ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Navigation;
using ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Security;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.Navigation;
using ITVComponents.WebCoreToolkit.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Extensions
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
            return services.AddDbContext<AspNetSecurityContext>(options)
                .RegisterExplicityInterfacesScoped<AspNetSecurityContext>()
                .AddScoped<ISecurityRepository, AspNetDbSecurityRepository>();
        }

        /// <summary>
        /// Enables DbIdentities with the default SecurityContext db-context
        /// </summary>
        /// <typeparam name="TImpl">the implementation-type if derived from the base SecurityContext - class</typeparam>
        /// <param name="services">the services where the SecurityContext is injected</param>
        /// <param name="options">the options for the context</param>
        /// <returns>the serviceCollection instance that was passed as argument</returns>
        public static IServiceCollection UseDbIdentities<TImpl>(this IServiceCollection services, Action<DbContextOptionsBuilder> options) where TImpl:AspNetSecurityContext
        {
            return services.AddDbContext<TImpl>(options)
                .RegisterExplicityInterfacesScoped<TImpl>()
                .AddScoped<ISecurityRepository, AspNetDbSecurityRepository>();
        }

        /// <summary>
        /// Activate DB-Navigation
        /// </summary>
        /// <param name="services">the Services-collection where to inject the DB-Navigation builder instance</param>
        /// <returns>the serviceCollection instance that was passed as argument</returns>
        public static IServiceCollection UseDbNavigation(this IServiceCollection services)
        {
            return services.AddScoped<INavigationBuilder, AspNetDbNavigationBuilder>();
        }
    }
}
