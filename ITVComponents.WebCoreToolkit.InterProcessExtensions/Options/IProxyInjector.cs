using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.InterProcessExtensions.Options
{
    /// <summary>
    /// Marker-Interface that is used to collect available Proxy-Injectors. !!Do not directly implement this interface. When required, derive from the ProxyInjector class!!
    /// </summary>
    public interface IProxyInjector
    {
        /// <summary>
        /// Creates a remote proxy
        /// </summary>
        /// <param name="services">the services collection providing required dependencies</param>
        /// <returns>the requested proxy instance</returns>
        object GetProxy(IServiceProvider services);
    }
}
