using System;
using ITVComponents.WebCoreToolkit.EntityFramework.DataAnnotations;
using ITVComponents.WebCoreToolkit.Localization;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.Localization;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.Options;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.ValidationAdapters;
using Microsoft.AspNetCore.Mvc.DataAnnotations;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.Extensions
{
    public static class DependencyExtensions
    {
        /// <summary>
        /// Activates client localization availability for client-scripts
        /// </summary>
        /// <param name="services">the services-collection in which the options are injected</param>
        /// <returns>the provided ServiceCollection for method-chaining</returns>
        public static IServiceCollection UseScriptLocalization(this IServiceCollection services)
        {
            return services.AddScoped(typeof(ILocalScriptProvider<>), typeof(ClientScriptResourceProvider<>));
        }

        /// <summary>
        /// Configures the usage of custom Validation adapters to enable unobtrusive javascript validation and localization
        /// </summary>
        /// <param name="services">the services-collection in which the options are injected</param>
        /// <param name="configure">configures the custom ValidationAttributes and their adapters</param>
        /// <returns>the provided ServiceCollection for method-chaining</returns>
        public static IServiceCollection UseValidationAdapters(this IServiceCollection services, Action<AttributeAdapterProviderOptions> configure = null)
        {
            return services.Configure<AttributeAdapterProviderOptions>(o =>
                {
                    o.TryAddType<ConditionalRequiredAttribute>((a, l) => new ConditionalRequiredAttributeAdapter(a, l));
                    o.TryAddType<ForceTrueAttribute>((a, l) => new ForceTrueAttributeAdapter(a, l));
                configure?.Invoke(o);
            })
                .AddSingleton<IValidationAttributeAdapterProvider, ConfigurableAttributeAdapterProvider>();
        }
    }
}
