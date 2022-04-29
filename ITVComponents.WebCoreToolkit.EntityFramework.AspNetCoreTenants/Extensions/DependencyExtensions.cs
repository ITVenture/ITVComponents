using System;
using ITVComponents.Scripting.CScript.Core.Methods;
using ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Helpers;
using ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Navigation;
using ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Security;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Helpers;
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
                .AddScoped<ISecurityRepository, AspNetDbSecurityRepository<AspNetSecurityContext>>()
                .AddScoped<ITenantTemplateHelper<AspNetSecurityContext>, TenantTemplateHelper<AspNetSecurityContext>>()
                .AddScoped<ITenantTemplateHelper, TenantTemplateHelper<AspNetSecurityContext>>();
        }

        /// <summary>
        /// Enables DbIdentities with the default SecurityContext db-context
        /// </summary>
        /// <typeparam name="TImpl">the implementation-type if derived from the base SecurityContext - class</typeparam>
        /// <param name="services">the services where the SecurityContext is injected</param>
        /// <param name="options">the options for the context</param>
        /// <returns>the serviceCollection instance that was passed as argument</returns>
        public static IServiceCollection UseDbIdentities<TImpl>(this IServiceCollection services, Action<DbContextOptionsBuilder> options) where TImpl:AspNetSecurityContext<TImpl>
        {
            return services.AddDbContext<TImpl>(options)
                .RegisterExplicityInterfacesScoped<TImpl>()
                .AddScoped<ISecurityRepository, AspNetDbSecurityRepository<TImpl>>()
                .AddScoped<ITenantTemplateHelper<TImpl>, TenantTemplateHelper<TImpl>>()
                .AddScoped<ITenantTemplateHelper, TenantTemplateHelper<TImpl>>();
        }

        /// <summary>
        /// Enables DbIdentities with the default SecurityContext db-context
        /// </summary>
        /// <param name="contextType">the implementation-type if derived from the base SecurityContext - class</param>
        /// <param name="services">the services where the SecurityContext is injected</param>
        /// <param name="options">the options for the context</param>
        /// <returns>the serviceCollection instance that was passed as argument</returns>
        public static IServiceCollection UseDbIdentities(this IServiceCollection services, Type contextType,
            Action<DbContextOptionsBuilder> options)
        {
            var method = MethodHelper.GetMethodInfo(() => UseDbIdentities<AspNetSecurityContext>(services,options))
                .GetGenericMethodDefinition();
            method = method.MakeGenericMethod(contextType);
            return (IServiceCollection)method.Invoke(null, new object[] { services, options });
        }

        /// <summary>
        /// Enables DbIdentities with the default SecurityContext db-context
        /// </summary>
        /// <typeparam name="TImpl">the implementation-type if derived from the base SecurityContext - class</typeparam>
        /// <param name="services">the services where the SecurityContext is injected</param>
        /// <param name="options">the options for the context</param>
        /// <returns>the serviceCollection instance that was passed as argument</returns>
        public static IServiceCollection UseDbIdentities<TImpl, TTmpHelper>(this IServiceCollection services, Action<DbContextOptionsBuilder> options)
            where TImpl : AspNetSecurityContext<TImpl>
            where TTmpHelper : TenantTemplateHelper<TImpl>
        {
            return services.AddDbContext<TImpl>(options)
                .RegisterExplicityInterfacesScoped<TImpl>()
                .AddScoped<ISecurityRepository, AspNetDbSecurityRepository<TImpl>>()
                .AddScoped<ITenantTemplateHelper<TImpl>, TTmpHelper>()
                .AddScoped<ITenantTemplateHelper, TTmpHelper>();
        }

        /// <summary>
        /// Activate DB-Navigation
        /// </summary>
        /// <param name="services">the Services-collection where to inject the DB-Navigation builder instance</param>
        /// <returns>the serviceCollection instance that was passed as argument</returns>
        public static IServiceCollection UseDbNavigation(this IServiceCollection services)
        {
            return services.AddScoped<INavigationBuilder, AspNetDbNavigationBuilder<AspNetSecurityContext>>();
        }

        /// <summary>
        /// Activate DB-Navigation
        /// </summary>
        /// <param name="services">the Services-collection where to inject the DB-Navigation builder instance</param>
        /// <param name="contextType">the target type of the db-context to use</param>
        /// <returns>the serviceCollection instance that was passed as argument</returns>
        public static IServiceCollection UseDbNavigation(this IServiceCollection services, Type contextType)
        {
            var t = typeof(AspNetDbNavigationBuilder<>).MakeGenericType(contextType);
            return services.AddScoped(typeof(INavigationBuilder), t);
        }

        /// <summary>
        /// Activate DB-Navigation
        /// </summary>
        /// <param name="services">the Services-collection where to inject the DB-Navigation builder instance</param>
        /// <returns>the serviceCollection instance that was passed as argument</returns>
        public static IServiceCollection UseDbNavigation<TImpl>(this IServiceCollection services)
        where TImpl:AspNetSecurityContext<TImpl>
        {
            return services.AddScoped<INavigationBuilder, AspNetDbNavigationBuilder<TImpl>>();
        }
    }
}
