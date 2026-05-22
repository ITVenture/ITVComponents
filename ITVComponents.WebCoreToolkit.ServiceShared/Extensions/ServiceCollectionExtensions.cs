using ITVComponents.WebCoreToolkit.ServiceShared.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.ServiceShared.Extensions
{
    /// <summary>
    /// Host-neutral registration of the shared WebCoreToolkit services. Callable from an MVC host as well as from a
    /// Blazor host, without requiring AddControllers/endpoint-mapping.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Registers the framework-neutral WebCoreToolkit services (currently the Diagnostics-Query service).
        /// </summary>
        /// <param name="services">the service-collection to extend</param>
        /// <returns>the same service-collection for chaining</returns>
        public static IServiceCollection AddWebCoreToolkitServiceShared(this IServiceCollection services)
        {
            return services.AddSingleton<IDiagnosticsQueryService, DiagnosticsQueryService>();
        }
    }
}
