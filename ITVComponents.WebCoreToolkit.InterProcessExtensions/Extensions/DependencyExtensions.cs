using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.InterProcessExtensions.Internal;
using ITVComponents.WebCoreToolkit.InterProcessExtensions.Options;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.InterProcessExtensions.Extensions
{
    public static class DependencyExtensions
    {
        /// <summary>
        /// Enables injectable Remote Proxies
        /// </summary>
        /// <param name="services">the service collection where the proxies are injected</param>
        /// <param name="options">the proxy options</param>
        /// <returns>the provided servicecollection for method chaining</returns>
        public static IServiceCollection UseInjectableProxies(this IServiceCollection services, Action<ProxyOptions> options)
        {
            return services.AddScoped(typeof(IInjectableProxy<>), typeof(InjectableProxyImpl<>))
                .Configure(options);
        }
    }
}
