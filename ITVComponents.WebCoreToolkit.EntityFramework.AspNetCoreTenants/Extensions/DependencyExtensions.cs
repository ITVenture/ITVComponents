using System;
using System.Linq.Expressions;
using Castle.DynamicProxy.Generators.Emitters.SimpleAST;
using ITVComponents.Helpers;
using ITVComponents.Scripting.CScript.Core.Methods;
using ITVComponents.Scripting.CScript.Helpers;
using ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Helpers;
using ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Navigation;
using ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Security;
using ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Security.ApplicationToken;
using ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Security.SharedAssets;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Helpers;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.Navigation;
using ITVComponents.WebCoreToolkit.Security;
using ITVComponents.WebCoreToolkit.Security.ApplicationToken;
using ITVComponents.WebCoreToolkit.Security.SharedAssets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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
        public static IServiceCollection UseDbIdentities(this IServiceCollection services, Action<IServiceProvider, DbContextOptionsBuilder> options)
        {
            var finaltth = typeof(AspNetSecurityContext).FinalizeType(typeof(ITenantTemplateHelper<,,,,,,>), fixTypeEntries: ("TContext",typeof(AspNetSecurityContext)));
            return services.AddDbContext<AspNetSecurityContext>(options)
                .RegisterExplicityInterfacesScoped<AspNetSecurityContext>()
                .AddScoped<ISecurityRepository>(i =>
                {
                    var retVal = new AspNetDbSecurityRepository<AspNetSecurityContext>(
                        i.GetService<AspNetSecurityContext>(),
                        i.GetService<ILogger<AspNetDbSecurityRepository<AspNetSecurityContext>>>());
                    return i.GetAssetSecurityRepository(retVal);

                })
                //.AddScoped<ITenantTemplateHelper<AspNetSecurityContext>, TenantTemplateHelper<AspNetSecurityContext>>()
                .AddScoped(finaltth, typeof(TenantTemplateHelper<AspNetSecurityContext>));
        }

        /// <summary>
        /// Enables DbIdentities with the default SecurityContext db-context
        /// </summary>
        /// <typeparam name="TImpl">the implementation-type if derived from the base SecurityContext - class</typeparam>
        /// <param name="services">the services where the SecurityContext is injected</param>
        /// <param name="options">the options for the context</param>
        /// <returns>the serviceCollection instance that was passed as argument</returns>
        public static IServiceCollection UseDbIdentities<TImpl>(this IServiceCollection services, Action<IServiceProvider, DbContextOptionsBuilder> options) where TImpl:AspNetSecurityContext<TImpl>
        {
            var finaltth = typeof(TImpl).FinalizeType(typeof(ITenantTemplateHelper<,,,,,,>), fixTypeEntries: ("TContext", typeof(TImpl)));
            return services.AddDbContext<TImpl>(options)
                    .RegisterExplicityInterfacesScoped<TImpl>()
                    .AddScoped<ISecurityRepository>(i =>
                    {
                        var retVal = new AspNetDbSecurityRepository<TImpl>(i.GetService<TImpl>(),
                                i.GetService<ILogger<AspNetDbSecurityRepository<TImpl>>>());
                        return i.GetAssetSecurityRepository(retVal);
                    })
                    //.AddScoped<ITenantTemplateHelper<TImpl>, TenantTemplateHelper<TImpl>>()
                    .AddScoped(finaltth, typeof(TenantTemplateHelper<TImpl>));
        }

        /*/// <summary>
        /// Enables DbIdentities with the default SecurityContext db-context
        /// </summary>
        /// <param name="contextType">the implementation-type if derived from the base SecurityContext - class</param>
        /// <param name="services">the services where the SecurityContext is injected</param>
        /// <param name="options">the options for the context</param>
        /// <returns>the serviceCollection instance that was passed as argument</returns>
        public static IServiceCollection UseDbIdentities(this IServiceCollection services, Type contextType,
            Action<IServiceProvider, DbContextOptionsBuilder> options)
        {
            var method = LambdaHelper.GetMethodInfo(() => UseDbIdentities<AspNetSecurityContext>(services,options))
                .GetGenericMethodDefinition();
            method = method.MakeGenericMethod(contextType);
            return (IServiceCollection)method.Invoke(null, new object[] { services, options });
        }*/

        /// <summary>
        /// Enables DbIdentities with the default SecurityContext db-context
        /// </summary>
        /// <typeparam name="TImpl">the implementation-type if derived from the base SecurityContext - class</typeparam>
        /// <param name="services">the services where the SecurityContext is injected</param>
        /// <param name="options">the options for the context</param>
        /// <returns>the serviceCollection instance that was passed as argument</returns>
        public static IServiceCollection UseDbIdentities<TImpl, TTmpHelper>(this IServiceCollection services, Action<IServiceProvider, DbContextOptionsBuilder> options)
            where TImpl : AspNetSecurityContext<TImpl>
            where TTmpHelper : TenantTemplateHelper<TImpl>
        {
            var finaltth = typeof(TImpl).FinalizeType(typeof(ITenantTemplateHelper<,,,,,,>), fixTypeEntries: ("TContext", typeof(TImpl)));
            return services.AddDbContext<TImpl>(options)
                .RegisterExplicityInterfacesScoped<TImpl>()
                .AddScoped<ISecurityRepository>(i =>
                {
                    var retVal = new AspNetDbSecurityRepository<TImpl>(i.GetService<TImpl>(),
                            i.GetService<ILogger<AspNetDbSecurityRepository<TImpl>>>());
                    return i.GetAssetSecurityRepository(retVal);
                })
                //.AddScoped<ITenantTemplateHelper<TImpl>, TTmpHelper>()
                .AddScoped(finaltth, typeof(TTmpHelper));
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

        /*/// <summary>
        /// Activate DB-Navigation
        /// </summary>
        /// <param name="services">the Services-collection where to inject the DB-Navigation builder instance</param>
        /// <param name="contextType">the target type of the db-context to use</param>
        /// <returns>the serviceCollection instance that was passed as argument</returns>
        public static IServiceCollection UseDbNavigation(this IServiceCollection services, Type contextType)
        {
            var t = typeof(AspNetDbNavigationBuilder<>).MakeGenericType(contextType);
            return services.AddScoped(typeof(INavigationBuilder), t);
        }*/

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

        /// <summary>
        /// Activate Db-Driven Shared Assets 
        /// </summary>
        /// <param name="services">the Services-collection where to inject the DB-Asset-handler instance</param>
        /// <returns>the serviceCollection instance that was passed as argument</returns>
        public static IServiceCollection UseDbSharedAssets(this IServiceCollection services)
        {
            return services.AddScoped<ISharedAssetAdapter, SharedAssetProvider>();
        }

        /*/// <summary>
        /// Activate Db-Driven Shared Assets 
        /// </summary>
        /// <param name="services">the Services-collection where to inject the DB-Asset-handler instance</param>
        /// <param name="contextType">the target type of the db-context to use</param>
        /// <returns>the serviceCollection instance that was passed as argument</returns>
        public static IServiceCollection UseDbSharedAssets(this IServiceCollection services, Type contextType)
        {
            var t = typeof(SharedAssetProvider<>).MakeGenericType(contextType);
            return services.AddScoped(typeof(ISharedAssetAdapter), t);
        }*/

        /// <summary>
        /// Activate Db-Driven Application Refresh Token services 
        /// </summary>
        /// <param name="services">the Services-collection where to inject the DB-AppToken-handler instance</param>
        /// <returns>the serviceCollection instance that was passed as argument</returns>
        public static IServiceCollection UseApplicationTokenService(this IServiceCollection services)
        {
            return services
                .AddScoped<IApplicationTokenService, ApplicationTokenService>();
        }

        /// <summary>
        /// Activate Db-Driven Application Refresh Token services 
        /// </summary>
        /// <param name="services">the Services-collection where to inject the DB-AppToken-handler instance</param>
        /// <param name="contextType">the target type of the db-context to use</param>
        /// <returns>the serviceCollection instance that was passed as argument</returns>
        public static IServiceCollection UseApplicationTokenService<TImpl>(this IServiceCollection services) 
            where TImpl : AspNetSecurityContext<TImpl>
        {
            return services.AddScoped<IApplicationTokenService, ApplicationTokenService<TImpl>>();
        }

        /// <summary>
        /// Activate Db-Driven Shared Assets 
        /// </summary>
        /// <param name="services">the Services-collection where to inject the DB-Asset-handler instance</param>
        /// <returns>the serviceCollection instance that was passed as argument</returns>
        public static IServiceCollection UseDbSharedAssets<TImpl>(this IServiceCollection services)
            where TImpl : AspNetSecurityContext<TImpl>
        {
            return services.AddScoped<ISharedAssetAdapter, SharedAssetProvider<TImpl>>();
        }
    }
}
