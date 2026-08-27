using System;
using ITVComponents.Helpers;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Helpers;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Navigation;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Security;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Security.ApplicationToken;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Security.SharedAssets;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.DependencyInjection;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.ExternalOAuthServices.Options;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Security.ApplicationToken;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.Navigation;
using ITVComponents.WebCoreToolkit.Security;
using ITVComponents.WebCoreToolkit.Security.ApplicationToken;
using ITVComponents.WebCoreToolkit.Security.ComponentTrust;
using ITVComponents.WebCoreToolkit.Security.SharedAssets;
using Microsoft.EntityFrameworkCore;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Extensions
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
            var finaltth = typeof(AspNetTreeSecurityContext).FinalizeType(typeof(ITenantTemplateHelper<,,,,,,,,,,>), fixTypeEntries: ("TContext",typeof(AspNetTreeSecurityContext)));
            return services.AddDbContext<AspNetTreeSecurityContext>(options)
                // Per-operation factory backing the (now factory-based) security repository.
                .AddScoped<IDbContextFactory<AspNetTreeSecurityContext>, ToolkitDbContextFactory<AspNetTreeSecurityContext>>()
                .AddScoped<IToolkitContextFactory, ToolkitContextFactory<AspNetTreeSecurityContext>>()
                .RegisterExplicityInterfacesScoped<AspNetTreeSecurityContext>()
                .AddScoped<ISecurityRepository>(i =>
                {
                    var retVal = new AspNetDbTreeSecurityRepository<AspNetTreeSecurityContext>(
                        i.GetService<IToolkitContextFactory>(),
                        i.GetService<ISecurityAccessProvider>(),
                        i.GetService<IOptions<ExternalOAuthServiceBufferingOptions>>(),
                        i.GetService<ILogger<AspNetDbTreeSecurityRepository<AspNetTreeSecurityContext>>>(),
                        i.GetService<ITVComponents.WebCoreToolkit.Caching.IEntityChangeSignal>());
                    return i.GetAssetSecurityRepository(retVal);

                })
                //.AddScoped<ITenantTemplateHelper<AspNetSecurityContext>, TenantTemplateHelper<AspNetSecurityContext>>()
                .AddScoped(finaltth, typeof(TenantTreeTemplateHelper<AspNetTreeSecurityContext>));
        }

        /// <summary>
        /// Enables DbIdentities with the default SecurityContext db-context
        /// </summary>
        /// <typeparam name="TImpl">the implementation-type if derived from the base SecurityContext - class</typeparam>
        /// <param name="services">the services where the SecurityContext is injected</param>
        /// <param name="options">the options for the context</param>
        /// <returns>the serviceCollection instance that was passed as argument</returns>
        public static IServiceCollection UseDbIdentities<TImpl>(this IServiceCollection services, Action<IServiceProvider, DbContextOptionsBuilder> options) where TImpl: AspNetTreeSecurityContext<TImpl>
        {
            var finaltth = typeof(TImpl).FinalizeType(typeof(ITenantTemplateHelper<,,,,,,,,,,>), fixTypeEntries: ("TContext", typeof(TImpl)));
            return services.AddDbContext<TImpl>(options)
                    // Phase 0 (IDbContextFactory-Migration): per-Operation-Factory NEBEN dem scoped Context.
                    // ActivatorUtilities-basiert, da der Default-EF-Factory unseren Mehr-Arg-Runtime-Ctor nicht
                    // bedienen kann. Scoped -> erzeugte Contexts bekommen Mandant/User-State. Additiv: bestehende
                    // (MVC + Blazor) Konsumenten laufen unverändert weiter.
                    .AddScoped<IDbContextFactory<TImpl>, ToolkitDbContextFactory<TImpl>>()
                    // Per-Operation ICoreSystemContext-Factory für Blazor-Admin-Handler (Phase 2+).
                    .AddScoped<ICoreSystemContextFactory, CoreSystemContextFactory<TImpl>>()
                    // Generische per-Operation-Factory: liefert den frischen Context als JEDES implementierte
                    // DbSet-Abstraktions-Interface (ICoreSystemContext/ISecurityContext/IBaseTenantContext/…).
                    .AddScoped<IToolkitContextFactory, ToolkitContextFactory<TImpl>>()
                    .RegisterExplicityInterfacesScoped<TImpl>()
                    .AddScoped<ISecurityRepository>(i =>
                    {
                        var retVal = new AspNetDbTreeSecurityRepository<TImpl>(i.GetService<IToolkitContextFactory>(),
                                i.GetService<ISecurityAccessProvider>(),
                            i.GetService<IOptions<ExternalOAuthServiceBufferingOptions>>(),
                                i.GetService<ILogger<AspNetDbTreeSecurityRepository<TImpl>>>(),
                                i.GetService<ITVComponents.WebCoreToolkit.Caching.IEntityChangeSignal>(),
                                i);
                        return i.GetAssetSecurityRepository(retVal);
                    })
                    //.AddScoped<ITenantTemplateHelper<TImpl>, TenantTemplateHelper<TImpl>>()
                    .AddScoped(finaltth, typeof(TenantTreeTemplateHelper<TImpl>));
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
            where TImpl : AspNetTreeSecurityContext<TImpl>
            where TTmpHelper : TenantTreeTemplateHelper<TImpl>
        {
            var finaltth = typeof(TImpl).FinalizeType(typeof(ITenantTemplateHelper<,,,,,,,,,,>), fixTypeEntries: ("TContext", typeof(TImpl)));
            return services.AddDbContext<TImpl>(options)
                .AddScoped<IDbContextFactory<TImpl>, ToolkitDbContextFactory<TImpl>>()
                .AddScoped<ICoreSystemContextFactory, CoreSystemContextFactory<TImpl>>()
                .AddScoped<IToolkitContextFactory, ToolkitContextFactory<TImpl>>()
                .RegisterExplicityInterfacesScoped<TImpl>()
                .AddScoped<ISecurityRepository>(i =>
                {
                    var retVal = new AspNetDbTreeSecurityRepository<TImpl>(i.GetService<IToolkitContextFactory>(),
                                i.GetService<ISecurityAccessProvider>(),
                        i.GetService<IOptions<ExternalOAuthServiceBufferingOptions>>(),
                            i.GetService<ILogger<AspNetDbTreeSecurityRepository<TImpl>>>(),
                            services: i);
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
            return services.AddScoped<INavigationBuilder, AspNetDbTreeNavigationBuilder<AspNetTreeSecurityContext>>();
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
        where TImpl: AspNetTreeSecurityContext<TImpl>
        {
            return services.AddScoped<INavigationBuilder, AspNetDbTreeNavigationBuilder<TImpl>>();
        }

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
            where TImpl : AspNetTreeSecurityContext<TImpl>
        {
            return services.AddScoped<IApplicationTokenService, ApplicationTokenService<TImpl>>();
        }

        /// <summary>
        /// Activate Db-Driven Shared Assets 
        /// </summary>
        /// <param name="services">the Services-collection where to inject the DB-Asset-handler instance</param>
        /// <returns>the serviceCollection instance that was passed as argument</returns>
        public static IServiceCollection UseDbSharedAssets<TImpl>(this IServiceCollection services)
            where TImpl : AspNetTreeSecurityContext<TImpl>
        {
            return services.UsePersistentAssetArgumentRegistry().UseDbAssetAccessLog().AddScoped<ISharedAssetAdapter, SharedAssetProvider<TImpl>>();
        }
    }
}
