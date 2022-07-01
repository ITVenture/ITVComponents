using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.InterProcessExtensions.Options
{
    public interface IDefaultProxyConfigurator: IProxyInjector
    {
        /// <summary>
        /// Gets or sets the name of the ProxyObject in the remote service
        /// </summary>
        public string ProxyName { get; set; }

        /// <summary>
        /// Gets or sets the UniqueName-Patterns for the injected proxy-client object. The last pattern must result in a IBaseClient instance
        /// </summary>
        public string[] ObjectPatterns { get; set; }
    }
}
