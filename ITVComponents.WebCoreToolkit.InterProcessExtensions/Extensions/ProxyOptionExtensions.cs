using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.InterProcessExtensions.Options;

namespace ITVComponents.WebCoreToolkit.InterProcessExtensions.Extensions
{
    public static class ProxyOptionExtensions
    {
        /// <summary>
        /// Configures a new proxy instance for the given ProxyOptions
        /// </summary>
        /// <typeparam name="T">the type for which a proxy must be created</typeparam>
        /// <param name="options">the options that are used for proxy-injection</param>
        /// <param name="configure">a callback that allows the custom configuration of the Proxy-settings</param>
        /// <returns>the proxy-options that were passed initially for method-chaining.</returns>
        public static ProxyOptions ConfigureProxy<T>(this ProxyOptions options, Action<DefaultProxyInjector<T>> configure) where T : class
        {
            var item = new DefaultProxyInjector<T>();
            options.AddInjector(item);
            configure(item);
            return options;
        }

        /// <summary>
        /// Configures a new proxy instance for the given ProxyOptions
        /// </summary>
        /// <typeparam name="T">the type for which a proxy must be created</typeparam>
        /// <param name="options">the options that are used for proxy-injection</param>
        /// <param name="configure">a callback that allows the custom configuration of the Proxy-settings</param>
        /// <returns>the proxy-options that were passed initially for method-chaining.</returns>
        public static ProxyOptions ConfigureProxy<T>(this ProxyOptions options, Func<ProxyInjector<T>> configure) where T : class
        {
            var item = configure();
            options.AddInjector(item);
            return options;
        }
    }
}
