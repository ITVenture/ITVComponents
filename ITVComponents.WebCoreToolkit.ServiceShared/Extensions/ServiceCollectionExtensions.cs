using ITVComponents.WebCoreToolkit.ServiceShared.Diagnostics;
using ITVComponents.WebCoreToolkit.ServiceShared.Service;
using ITVComponents.WebCoreToolkit.ServiceShared.Service.Impl;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ITVComponents.WebCoreToolkit.ServiceShared.Extensions
{
    /// <summary>
    /// Host-neutral registration of the shared WebCoreToolkit services. Callable from an MVC host as well as from a
    /// Blazor host, without requiring AddControllers/endpoint-mapping.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Registers the framework-neutral WebCoreToolkit services (the Diagnostics-Query service and the shared
        /// file up-/download service handler).
        /// </summary>
        /// <param name="services">the service-collection to extend</param>
        /// <returns>the same service-collection for chaining</returns>
        public static IServiceCollection AddWebCoreToolkitServiceShared(this IServiceCollection services)
        {
            services.AddSingleton<IDiagnosticsQueryService, DiagnosticsQueryService>();
            // Scoped: the handler resolves scoped collaborators through the injected IServiceProvider.
            // TryAdd keeps it idempotent with the WebPart-discovered registration on an MVC host.
            services.TryAddScoped<IFileServiceHandler, DefaultFileServiceHandler>();
            return services;
        }
    }
}
