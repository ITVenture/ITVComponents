using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using ITVComponents.WebCoreToolkit.Middleware;
using ITVComponents.WebCoreToolkit.Security.SharedAssets;
using ITVComponents.WebCoreToolkit.Options;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.Extensions
{
    public static class AppBuilderExtensions
    {
        /// <summary>
        /// Configures the app, that all Requests are considered save
        /// </summary>
        /// <param name="app">the applicationBuilder object</param>
        public static IApplicationBuilder AllRequestsSecure(this IApplicationBuilder app)
        {
            return app.Use((context, next) =>
            {
                context.Request.Scheme = "https";
                return next();
            });
        }

        /// <summary>
        /// Enables thread-Cultures. For this to work, UseRequestLocalization is also required to be called
        /// </summary>
        /// <param name="app">the applicationbuilder object</param>
        public static IApplicationBuilder EnableThreadCultures(this IApplicationBuilder app)
        {
            return app.UseMiddleware<ThreadCultureMiddleware>();
        }

        /// <summary>
        /// Registers the <see cref="SharedAssetPathMiddleware"/>, which turns the first path segment
        /// <c>/~{key}[.{token}]/…</c> into the shared-asset context of the request and moves it into
        /// <c>PathBase</c>.
        /// <para>
        /// Place it <b>as early as possible - before everything else</b>: before <c>UseStaticFiles</c>,
        /// <c>UseAuthentication</c>, <c>UseRouting</c> and <c>UseTenantPathPrefix</c>. One registration is
        /// all it takes; unlike the tenant prefix, this segment leads the URL, so recognizing and stripping
        /// happen in the same step.
        /// </para>
        /// <para>
        /// Requires the services behind <c>WebPartInitOptions.UseSharedAssets</c> (or an explicit
        /// <c>services.UseSharedAssetPathContext()</c>).
        /// </para>
        /// </summary>
        /// <param name="app">the applicationbuilder object</param>
        /// <returns>the application builder for chaining</returns>
        public static IApplicationBuilder UseSharedAssetPath(this IApplicationBuilder app)
        {
            return app.UseMiddleware<SharedAssetPathMiddleware>();
        }
    }
}
