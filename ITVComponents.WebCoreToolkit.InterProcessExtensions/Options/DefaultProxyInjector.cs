using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Formatting;
using ITVComponents.InterProcessCommunication.Shared.Base;
using ITVComponents.Plugins;
using ITVComponents.WebCoreToolkit.Security;
using ITVComponents.WebCoreToolkit.WebPlugins;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.InterProcessExtensions.Options
{
    /// <summary>
    /// Default-Implementation of a ProxyInjector
    /// </summary>
    /// <typeparam name="T">the expected return type</typeparam>
    public class DefaultProxyInjector<T>:ProxyInjector<T> where T:class
    {
        /// <summary>
        /// Gets or sets the name of the ProxyObject in the remote service
        /// </summary>
        public string ProxyName{get; set; }

        /// <summary>
        /// Gets or sets the UniqueName-Patterns for the injected proxy-client object. The last pattern must result in a IBaseClient instance
        /// </summary>
        public string[] ObjectPatterns{ get; set; }

        /// <summary>
        /// Gets the Proxy-Remote-Client that enables this object to create a proxy
        /// </summary>
        /// <param name="services">the service-collection that contains services required to create the client</param>
        /// <returns>the client-instance that is requested</returns>
        protected override IBaseClient GetClient(IServiceProvider services)
        {
            var userProvider = services.GetService<IContextUserProvider>();
            if (userProvider == null)
            {
                throw new InvalidOperationException("HttpContextAccessor is required!");
            }

            var plugins = services.GetService<IWebPluginHelper>();
            if (plugins == null)
            {
                throw new InvalidOperationException("Plugins are not configured!");
            }

            IPermissionScope nameExtender = services.GetService<IPermissionScope>();
            var permissionScope = nameExtender?.PermissionPrefix??"";
            Dictionary<string, object> formatHints = new Dictionary<string, object>(userProvider.RouteData);
            PluginFactory factory = plugins.GetFactory();
            IBaseClient retVal = null;
            formatHints.Add("PermissionScope", permissionScope);
            for (var i = 0; i < ObjectPatterns.Length; i++)
            {
                var name = ObjectPatterns[i];
                name = formatHints.FormatText(name);
                var tmp = factory[name, true];
                if (i == ObjectPatterns.Length - 1)
                {
                    retVal = (IBaseClient)tmp;
                }
            }

            return retVal;
        }

        /// <summary>
        /// Gets the name of the remote-object of which a proxy must be created
        /// </summary>
        /// <param name="services">the service-collection that contains services required to estimate the name</param>
        /// <returns>the estimated proxy-name</returns>
        protected override string GetProxyName(IServiceProvider services)
        {
            return ProxyName;
        }
    }
}
