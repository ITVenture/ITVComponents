using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.InterProcessCommunication.Shared.Security;
using ITVComponents.WebCoreToolkit.InterProcessExtensions.Internal;
using ITVComponents.WebCoreToolkit.InterProcessExtensions.Options;
using ITVComponents.WebCoreToolkit.InterProcessExtensions.Security;
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

        /// <summary>
        /// Initializes injectable security for the local GrpcService of a web application
        /// </summary>
        /// <param name="services">a servicecollection that is used to inject services to modules of the current application</param>
        /// <returns>the services collection that was passed as parameter</returns>
        public static IServiceCollection UseInjectableSecurity(this IServiceCollection services)
        {
            return services.AddScoped<ICustomServerSecurity, ServiceSecurityValidator>();
        }
    }
}
