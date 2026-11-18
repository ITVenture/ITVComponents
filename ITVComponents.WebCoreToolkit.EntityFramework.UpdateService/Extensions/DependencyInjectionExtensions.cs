using System;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.UpdateService.Administration;
using ITVComponents.WebCoreToolkit.EntityFramework.UpdateService.Feed;
using ITVComponents.WebCoreToolkit.EntityFramework.UpdateService.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ITVComponents.WebCoreToolkit.EntityFramework.UpdateService.Extensions
{
    /// <summary>
    /// Registers the web side of the update service.
    /// </summary>
    public static class DependencyInjectionExtensions
    {
        /// <summary>
        /// Registers the feed and the administration of releases, with the packages in the database (blocks of 1 MB).
        /// </summary>
        /// <remarks>
        /// The host provides an <see cref="IDbContextFactory{TContext}"/>, the tables
        /// (<c>modelBuilder.ConfigureUpdateService&lt;TTenant, TClientApp, TClientAppTemplate&gt;()</c>) and maps the feed
        /// (<c>app.MapUpdateFeed&lt;TContext&gt;()</c>). To sign the feed, it registers an
        /// <see cref="ITVComponents.UpdateService.Abstractions.Feed.IFeedSigner"/> (§114).
        /// </remarks>
        public static IServiceCollection AddUpdateServiceWithDatabaseStore<TContext, TClientApp, TClientAppTemplate>(this IServiceCollection services)
            where TContext : DbContext, IUpdateServiceContext, IClientAppContext<TClientApp, TClientAppTemplate>
            where TClientApp : class, IClientAppCore
            where TClientAppTemplate : class, IClientAppTemplateCore
        {
            services.TryAddSingleton<IUpdatePackageStore, DatabaseUpdatePackageStore<TContext>>();
            return AddCore<TContext, TClientApp, TClientAppTemplate>(services);
        }

        /// <summary>
        /// Registers the feed and the administration of releases, with the packages as files below a folder. The folder must
        /// survive a restart of the container and be shared by all instances.
        /// </summary>
        public static IServiceCollection AddUpdateServiceWithFileStore<TContext, TClientApp, TClientAppTemplate>(this IServiceCollection services, string packageDirectory)
            where TContext : DbContext, IUpdateServiceContext, IClientAppContext<TClientApp, TClientAppTemplate>
            where TClientApp : class, IClientAppCore
            where TClientAppTemplate : class, IClientAppTemplateCore
        {
            if (string.IsNullOrWhiteSpace(packageDirectory))
            {
                throw new ArgumentNullException(nameof(packageDirectory));
            }

            services.TryAddSingleton<IUpdatePackageStore>(new FileUpdatePackageStore(packageDirectory));
            return AddCore<TContext, TClientApp, TClientAppTemplate>(services);
        }

        private static IServiceCollection AddCore<TContext, TClientApp, TClientAppTemplate>(IServiceCollection services)
            where TContext : DbContext, IUpdateServiceContext, IClientAppContext<TClientApp, TClientAppTemplate>
            where TClientApp : class, IClientAppCore
            where TClientAppTemplate : class, IClientAppTemplateCore
        {
            services.TryAddSingleton<UpdateFeedBuilder<TContext>>();
            services.TryAddScoped<IUpdateReleaseAdministration, UpdateReleaseAdministration<TContext, TClientApp, TClientAppTemplate>>();
            return services;
        }
    }
}
