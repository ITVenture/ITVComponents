using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.EFRepo.Options;
using ITVComponents.Helpers;
using ITVComponents.Scripting.CScript.Core.Methods;
using ITVComponents.Scripting.CScript.Helpers;
using ITVComponents.WebCoreToolkit.Configuration;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Basic.Helpers;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Basic.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Basic.Navigation;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Basic.Security;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Basic.Security.SharedAssets;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.Logging;
using ITVComponents.WebCoreToolkit.Navigation;
using ITVComponents.WebCoreToolkit.Security;
using ITVComponents.WebCoreToolkit.Security.SharedAssets;
using ITVComponents.WebCoreToolkit.WebPlugins;
using Microsoft.EntityFrameworkCore;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Extensions;
using Microsoft.Extensions.DependencyInjection;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Basic.Extensions
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
            var finaltth = typeof(SecurityContext).FinalizeType(typeof(ITenantTemplateHelper<,,,,,,,,,,>), fixTypeEntries: ("TContext", typeof(SecurityContext)));
            return services.AddDbContext<SecurityContext>(options)
                .AddScoped<IDbContextFactory<SecurityContext>, ToolkitDbContextFactory<SecurityContext>>()
                .AddScoped<ICoreSystemContextFactory, CoreSystemContextFactory<SecurityContext>>()
                .AddScoped<IToolkitContextFactory, ToolkitContextFactory<SecurityContext>>()
                .RegisterExplicityInterfacesScoped<SecurityContext>()
                .AddScoped<ISecurityRepository>(i =>
                {
                    var retVal = new DbSecurityRepository<SecurityContext>(i.GetService<IToolkitContextFactory>(),
                            i.GetService<ILogger<DbSecurityRepository<SecurityContext>>>(),
                            i.GetService<ITVComponents.WebCoreToolkit.Caching.IEntityChangeSignal>());
                    return i.GetAssetSecurityRepository(retVal);
                })
                //.AddScoped<ITenantTemplateHelper<SecurityContext>, TenantTemplateHelper<SecurityContext>>()
                .AddScoped(finaltth,typeof(TenantTemplateHelper<SecurityContext>));
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
            var method = LambdaHelper.GetMethodInfo(() => UseDbIdentities<SecurityContext>(services, options))
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
        public static IServiceCollection UseDbIdentities<TImpl>(this IServiceCollection services, Action<IServiceProvider, DbContextOptionsBuilder> options) where TImpl:SecurityContext<TImpl>
        {
            var finaltth = typeof(TImpl).FinalizeType(typeof(ITenantTemplateHelper<,,,,,,,,,,>), fixTypeEntries: ("TContext", typeof(TImpl)));
            return services.AddDbContext<TImpl>(options)
                .AddScoped<IDbContextFactory<TImpl>, ToolkitDbContextFactory<TImpl>>()
                .AddScoped<ICoreSystemContextFactory, CoreSystemContextFactory<TImpl>>()
                .AddScoped<IToolkitContextFactory, ToolkitContextFactory<TImpl>>()
                .RegisterExplicityInterfacesScoped<TImpl>()
                .AddScoped<ISecurityRepository>(i =>
                {
                    var retVal = new DbSecurityRepository<TImpl>(i.GetService<IToolkitContextFactory>(),
                            i.GetService<ILogger<DbSecurityRepository<TImpl>>>(),
                            i.GetService<ITVComponents.WebCoreToolkit.Caching.IEntityChangeSignal>());
                    return i.GetAssetSecurityRepository(retVal);
                })
                //.AddScoped<ITenantTemplateHelper<TImpl>, TenantTemplateHelper<TImpl>>()
                .AddScoped(finaltth, typeof(TenantTemplateHelper<TImpl>));
        }

        /// <summary>
        /// Enables DbIdentities with the default SecurityContext db-context
        /// </summary>
        /// <typeparam name="TImpl">the implementation-type if derived from the base SecurityContext - class</typeparam>
        /// <param name="services">the services where the SecurityContext is injected</param>
        /// <param name="options">the options for the context</param>
        /// <returns>the serviceCollection instance that was passed as argument</returns>
        public static IServiceCollection UseDbIdentities<TImpl, TTmpHelper>(this IServiceCollection services, Action<IServiceProvider, DbContextOptionsBuilder> options) 
            where TImpl : SecurityContext<TImpl>
            where TTmpHelper: TenantTemplateHelper<TImpl>
        {
            var finaltth = typeof(TImpl).FinalizeType(typeof(ITenantTemplateHelper<,,,,,,,,,,>), fixTypeEntries: ("TContext", typeof(TImpl)));
            return services.AddDbContext<TImpl>(options)
                .AddScoped<IDbContextFactory<TImpl>, ToolkitDbContextFactory<TImpl>>()
                .AddScoped<ICoreSystemContextFactory, CoreSystemContextFactory<TImpl>>()
                .AddScoped<IToolkitContextFactory, ToolkitContextFactory<TImpl>>()
                .RegisterExplicityInterfacesScoped<TImpl>()
                .AddScoped<ISecurityRepository>(i =>
                {
                    var retVal = new DbSecurityRepository<TImpl>(i.GetService<IToolkitContextFactory>(),
                            i.GetService<ILogger<DbSecurityRepository<TImpl>>>(),
                            i.GetService<ITVComponents.WebCoreToolkit.Caching.IEntityChangeSignal>());
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

        /*/// <summary>
        /// Activate DB-Navigation
        /// </summary>
        /// <param name="services">the Services-collection where to inject the DB-Navigation builder instance</param>
        /// <param name="contextType">the target type of the db-context to use</param>
        /// <returns>the serviceCollection instance that was passed as argument</returns>
        public static IServiceCollection UseDbNavigation(this IServiceCollection services, Type contextType)
        {
            var t = typeof(DbNavigationBuilder<>).MakeGenericType(contextType);
            return services.AddScoped(typeof(INavigationBuilder), t);
        }*/

        /// <summary>
        /// Activate Db-Driven Shared Assets 
        /// </summary>
        /// <param name="services">the Services-collection where to inject the DB-Asset-handler instance</param>
        /// <returns>the serviceCollection instance that was passed as argument</returns>
        public static IServiceCollection UseDbSharedAssets(this IServiceCollection services)
        {
            return services.UsePersistentAssetArgumentRegistry().UseDbAssetAccessLog().AddScoped<ISharedAssetAdapter, SharedAssetProvider>();
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
            return services.UsePersistentAssetArgumentRegistry().UseDbAssetAccessLog().AddScoped(typeof(ISharedAssetAdapter), t);
        }*/

        /// <summary>
        /// Activate Db-Driven Shared Assets 
        /// </summary>
        /// <param name="services">the Services-collection where to inject the DB-Asset-handler instance</param>
        /// <returns>the serviceCollection instance that was passed as argument</returns>
        public static IServiceCollection UseDbSharedAssets<TImpl>(this IServiceCollection services)
            where TImpl : SecurityContext<TImpl>
        {
            return services.UsePersistentAssetArgumentRegistry().UseDbAssetAccessLog().AddScoped<ISharedAssetAdapter, SharedAssetProvider<TImpl>>();
        }
    }
}
