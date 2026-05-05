using System.Reflection;
using ITVComponents.WebCoreToolkit.Blazor.Configuration;
using ITVComponents.WebCoreToolkit.Blazor.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.Blazor.Extensions
{
    public static class DependencyExtensions
    {
        public static IServiceCollection ConfigureStubComponents(this IServiceCollection services, Action<StubComponentConfiguration> configure)
        {
            return services.Configure(configure);
        }

        /// <summary>
        /// Adds the given assembly to <see cref="BlazorRoutingOptions.AdditionalAssemblies"/>
        /// so the host's Blazor Router picks up its routable components.
        /// Idempotent: calling twice with the same assembly is a no-op.
        /// </summary>
        public static IServiceCollection AddBlazorRoutingAssembly(this IServiceCollection services, Assembly assembly)
        {
            services.Configure<BlazorRoutingOptions>(o => o.AddAssembly(assembly));
            return services;
        }
    }
}
