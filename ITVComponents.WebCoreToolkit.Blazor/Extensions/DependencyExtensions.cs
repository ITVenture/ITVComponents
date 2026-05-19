using System.Reflection;
using ITVComponents.WebCoreToolkit.AspExtensions.Options;
using ITVComponents.WebCoreToolkit.Blazor.Configuration;
using ITVComponents.WebCoreToolkit.Blazor.Localization;
using ITVComponents.WebCoreToolkit.Blazor.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.Blazor.Extensions
{
    public static class DependencyExtensions
    {
        /// <summary>
        /// Registers <see cref="IAttributeMessageLocalizer"/> so that the
        /// <c>ToolkitDataAnnotationsValidator</c> component can translate topic-prefixed
        /// DataAnnotation error messages on Blazor models. Requires
        /// <c>ConfigureAttributeTranslation()</c> to have been called beforehand so that
        /// <see cref="ITVComponents.WebCoreToolkit.Options.AttributeTranslationOptions"/> is in the container.
        /// </summary>
        /// <param name="services">the service collection to register into</param>
        /// <returns>the service collection for chaining</returns>
        public static IServiceCollection UseBlazorAttributeMessages(this IServiceCollection services)
        {
            services.AddSingleton<IAttributeMessageLocalizer, AttributeMessageLocalizer>();
            return services;
        }

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

        /// <summary>
        /// Adds the given assembly with a per-type filter. The filter is consumed at
        /// render-time by <c>FilteredRouteView</c> to short-circuit excluded pages
        /// to NotFound. Mirrors the MVC <c>AssemblyPartWithGenerics</c> blacklist
        /// so the same JSON config can be reused.
        /// </summary>
        public static IServiceCollection AddBlazorRoutingAssembly(
            this IServiceCollection services,
            Assembly assembly,
            AssemblyPartTypeLoadBehaviorOptions? typeFilter)
        {
            services.Configure<BlazorRoutingOptions>(o => o.AddAssembly(assembly, typeFilter));
            return services;
        }
    }
}
