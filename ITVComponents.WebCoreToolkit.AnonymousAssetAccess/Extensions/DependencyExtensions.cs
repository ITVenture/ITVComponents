using ITVComponents.WebCoreToolkit.Security.SharedAssets;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.AnonymousAssetAccess.Extensions
{
    public static class DependencyExtensions
    {
        /// <summary>
        /// Activates the default-apikey user-mapper
        /// </summary>
        /// <param name="services">the servicecollection to inject the resolver into</param>
        /// <returns>the provided servicecollection</returns>
        public static IServiceCollection UseDefaultAnonymousAssetResolver(this IServiceCollection services)
        {
            return services.AddScoped<IGetAnonymousAssetQuery, DefaultAnonymousAssetUserResolver>().AddScoped<IAnonymousAssetLinkProvider,DefaultAnonymousAssetUserResolver>();
        }
    }
}
