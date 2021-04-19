using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Configuration;
using ITVComponents.WebCoreToolkit.EntityFramework.DiagnosticsQueries;
using ITVComponents.WebCoreToolkit.EntityFramework.Options.Diagnostics;
using ITVComponents.WebCoreToolkit.EntityFramework.Options.ForeignKeys;
using ITVComponents.WebCoreToolkit.Logging;
using ITVComponents.WebCoreToolkit.Navigation;
using ITVComponents.WebCoreToolkit.Security;
using ITVComponents.WebCoreToolkit.WebPlugins;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Extensions
{
    public static class DependencyExtensions
    {
        /// <summary>
        /// Configures the Foreignkey - EndPoint
        /// </summary>
        /// <param name="services">the services-collection in which the options are injected</param>
        /// <param name="options">a callback for options</param>
        /// <returns>the provided ServiceCollection for method-chaining</returns>
        public static IServiceCollection ConfigureForeignKeys(this IServiceCollection services, Action<ForeignKeySourceOptions> options)
        {
            return services.Configure(options);
        }

        /// <summary>
        /// Configures the Diagnostics - EndPoint
        /// </summary>
        /// <param name="services">the services-collection in which the options are injected</param>
        /// <param name="options">a callback for options</param>
        /// <returns>the provided ServiceCollection for method-chaining</returns>
        public static IServiceCollection ConfigureDiagnosticsQueries<TDiagnosticsStore>(this IServiceCollection services, Action<DiagnosticsSourceOptions> options) where TDiagnosticsStore : class, IDiagnosticsQueryStore
        {
            return services.Configure(options).AddTransient<IDiagnosticsQueryStore,TDiagnosticsStore>();
        }
    }
}
