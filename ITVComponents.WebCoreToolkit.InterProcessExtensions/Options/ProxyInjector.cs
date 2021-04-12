using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.InterProcessCommunication.Shared.Base;

namespace ITVComponents.WebCoreToolkit.InterProcessExtensions.Options
{
    /// <summary>
    /// Base implementation of a proxy-injector
    /// </summary>
    /// <typeparam name="T">the proxy type that is created with this injector</typeparam>
    public abstract class ProxyInjector<T>:IProxyInjector where T:class
    {
        /// <summary>
        /// Gets the Proxy-Remote-Client that enables this object to create a proxy
        /// </summary>
        /// <param name="services">the service-collection that contains services required to create the client</param>
        /// <returns>the client-instance that is requested</returns>
        protected abstract IBaseClient GetClient(IServiceProvider services);

        /// <summary>
        /// Gets the name of the remote-object of which a proxy must be created
        /// </summary>
        /// <param name="services">the service-collection that contains services required to estimate the name</param>
        /// <returns>the estimated proxy-name</returns>
        protected abstract string GetProxyName(IServiceProvider services);

        /// <summary>
        /// Creates a remote proxy
        /// </summary>
        /// <param name="services">the services collection providing required dependencies</param>
        /// <returns>the requested proxy instance</returns>
        object IProxyInjector.GetProxy(IServiceProvider services)
        {
            var client = GetClient(services);
            return client.CreateProxy<T>(GetProxyName(services));
        }

        /// <summary>
        /// Creates a remote proxy
        /// </summary>
        /// <param name="services">the services collection providing required dependencies</param>
        /// <returns>the requested proxy instance</returns>
        public virtual T GetProxyInstance(IServiceProvider services)
        {
            return (T)((IProxyInjector)this).GetProxy(services);
        }
    }
}
