using ITVComponents.WebCoreToolkit.Localization;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.Localization;
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
            return services.AddTransient(typeof(ILocalScriptProvider<>), typeof(ClientScriptResourceProvider<>));
        }
    }
}
