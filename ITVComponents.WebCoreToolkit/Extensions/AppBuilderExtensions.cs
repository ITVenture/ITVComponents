using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using ITVComponents.WebCoreToolkit.Middleware;
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
    }
}
