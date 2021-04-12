using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.InterProcessExtensions.Options;
using ITVComponents.WebCoreToolkit.WebPlugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ITVComponents.WebCoreToolkit.InterProcessExtensions.Internal
{
    /// <summary>
    /// Implements an automatic proxy-loader
    /// </summary>
    /// <typeparam name="T">the proxy type to load</typeparam>
    internal class InjectableProxyImpl<T>:IInjectableProxy<T> where T:class
    {
        /// <summary>
        /// the service-provider that holds all current services
        /// </summary>
        private readonly IServiceProvider services;

        /// <summary>
        /// The configured proxy-load - options
        /// </summary>
        private readonly IOptions<ProxyOptions> options;

        /// <summary>
        /// the proxy instance
        /// </summary>
        private T proxy;

        /// <summary>
        /// Initializes a new instance of the InjectableProxyImpl class
        /// </summary>
        /// <param name="services">the dependency services used to initialize other objects</param>
        /// <param name="options">the proxy-options that are used to initialize proxy objects</param>
        public InjectableProxyImpl(IServiceProvider services, IOptions<ProxyOptions> options)
        {
            this.services = services;
            this.options = options;
        }

        /// <summary>
        /// Gets the remote instance as proxy-object
        /// </summary>
        public T Value => proxy ??= GetProxy();

        /// <summary>
        /// Creates the proxy-instance
        /// </summary>
        /// <returns>the created proxy</returns>
        private T GetProxy()
        {
            return options.Value.GetProxy<T>(services);
        }
    }
}
