using ITVComponents.WebCoreToolkit.AspExtensions;
using ITVComponents.WebCoreToolkit.AspExtensions.Impl;
using ITVComponents.WebCoreToolkit.ServiceShared.Service;
using ITVComponents.WebCoreToolkit.ServiceShared.Service.Impl;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ITVComponents.WebCoreToolkit.ServiceShared
{
    /// <summary>
    /// Host-neutral WebPart-registration for the shared file-services. Registers the framework-neutral
    /// <see cref="IFileServiceHandler"/> (consumed identically from an MVC host and a Blazor host) so the
    /// upload-pipeline is available without an explicit registration call.
    /// </summary>
    [WebPart]
    public static class WebPartInit
    {
        /// <summary>
        /// Registers the default file-service handler that drives the framework-neutral upload-pipeline.
        /// </summary>
        /// <param name="services">the service-collection to extend</param>
        [ServiceRegistrationMethod]
        public static void RegisterServices(IServiceCollection services)
        {
            // Scoped: the handler resolves scoped collaborators (IImpersonationControl, the resolved
            // FileHandler-plugin, IPermissionScope) through the injected IServiceProvider.
            services.TryAddScoped<IFileServiceHandler, DefaultFileServiceHandler>();
        }
    }
}
