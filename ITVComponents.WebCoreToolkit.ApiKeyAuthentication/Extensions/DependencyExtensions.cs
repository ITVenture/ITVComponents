using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.ApiKeyAuthentication.Extensions
{
    public static class DependencyExtensions
    {
        /// <summary>
        /// Activates the default-apikey user-mapper
        /// </summary>
        /// <param name="services">the servicecollection to inject the resolver into</param>
        /// <returns>the provided servicecollection</returns>
        public static IServiceCollection UseDefaultApiKeyResolver(this IServiceCollection services)
        {
            return services.AddScoped<IGetApiKeyQuery, DefaultApiKeyUserResolver>();
        }
    }
}
