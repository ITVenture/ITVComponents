using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.Security.SharedAssets;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.Extras.AnonymousAssetAccess.Extensions
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
            // Der Auth-Handler liest den Schluessel ueber den ISharedAssetContext - ohne ihn scheitert er
            // erst beim Anmelden und mit einer DI-Meldung, die nichts ueber die Ursache sagt.
            return services.UseSharedAssetPathContext()
                .AddScoped<IGetAnonymousAssetQuery, DefaultAnonymousAssetUserResolver>()
                .AddScoped<IAnonymousAssetLinkProvider, DefaultAnonymousAssetUserResolver>();
        }
    }
}
