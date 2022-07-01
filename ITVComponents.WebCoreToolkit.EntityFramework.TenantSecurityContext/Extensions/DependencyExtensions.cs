using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Scripting.CScript.Core.Methods;
using ITVComponents.WebCoreToolkit.Configuration;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.Helpers;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.Navigation;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.Security;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.Security.SharedAssets;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Helpers;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.Logging;
using ITVComponents.WebCoreToolkit.Navigation;
using ITVComponents.WebCoreToolkit.Security;
using ITVComponents.WebCoreToolkit.Security.SharedAssets;
using ITVComponents.WebCoreToolkit.WebPlugins;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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
                .AddScoped<ISecurityRepository>(i =>
                {
                    var retVal = new DbSecurityRepository<SecurityContext>(i.GetService<SecurityContext>(),
                            i.GetService<ILogger<DbSecurityRepository<SecurityContext>>>());
                    return i.GetAssetSecurityRepository(retVal);
                })
                .AddScoped<ITenantTemplateHelper<SecurityContext>, TenantTemplateHelper<SecurityContext>>()
                .AddScoped<ITenantTemplateHelper, TenantTemplateHelper<SecurityContext>>();
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
            var method = MethodHelper.GetMethodInfo(() => UseDbIdentities<SecurityContext>(services, options))
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
        public static IServiceCollection UseDbIdentities<TImpl>(this IServiceCollection services, Action<DbContextOptionsBuilder> options) where TImpl:SecurityContext<TImpl>
        {
            return services.AddDbContext<TImpl>(options)
                .RegisterExplicityInterfacesScoped<TImpl>()
                .AddScoped<ISecurityRepository>(i =>
                {
                    var retVal = new DbSecurityRepository<TImpl>(i.GetService<TImpl>(),
                            i.GetService<ILogger<DbSecurityRepository<TImpl>>>());
                    return i.GetAssetSecurityRepository(retVal);
                })
                .AddScoped<ITenantTemplateHelper<TImpl>, TenantTemplateHelper<TImpl>>()
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
                .AddScoped<ISecurityRepository>(i =>
                {
                    var retVal = new DbSecurityRepository<TImpl>(i.GetService<TImpl>(),
                            i.GetService<ILogger<DbSecurityRepository<TImpl>>>());
                    return i.GetAssetSecurityRepository(retVal);
                })
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

        /// <summary>
        /// Activate DB-Navigation
        /// </summary>
        /// <param name="services">the Services-collection where to inject the DB-Navigation builder instance</param>
        /// <param name="contextType">the target type of the db-context to use</param>
        /// <returns>the serviceCollection instance that was passed as argument</returns>
        public static IServiceCollection UseDbNavigation(this IServiceCollection services, Type contextType)
        {
            var t = typeof(DbNavigationBuilder<>).MakeGenericType(contextType);
            return services.AddScoped(typeof(INavigationBuilder), t);
        }

        /// <summary>
        /// Activate Db-Driven Shared Assets 
        /// </summary>
        /// <param name="services">the Services-collection where to inject the DB-Asset-handler instance</param>
        /// <returns>the serviceCollection instance that was passed as argument</returns>
        public static IServiceCollection UseDbSharedAssets(this IServiceCollection services)
        {
            return services.AddScoped<ISharedAssetAdapter, SharedAssetProvider>();
        }

        /// <summary>
        /// Activate Db-Driven Shared Assets 
        /// </summary>
        /// <param name="services">the Services-collection where to inject the DB-Asset-handler instance</param>
        /// <param name="contextType">the target type of the db-context to use</param>
        /// <returns>the serviceCollection instance that was passed as argument</returns>
        public static IServiceCollection UseDbSharedAssets(this IServiceCollection services, Type contextType)
        {
            var t = typeof(SharedAssetProvider<>).MakeGenericType(contextType);
            return services.AddScoped(typeof(ISharedAssetAdapter), t);
        }

        /// <summary>
        /// Activate Db-Driven Shared Assets 
        /// </summary>
        /// <param name="services">the Services-collection where to inject the DB-Asset-handler instance</param>
        /// <returns>the serviceCollection instance that was passed as argument</returns>
        public static IServiceCollection UseDbSharedAssets<TImpl>(this IServiceCollection services)
            where TImpl : SecurityContext<TImpl>
        {
            return services.AddScoped<ISharedAssetAdapter, SharedAssetProvider<TImpl>>();
        }
    }
}
