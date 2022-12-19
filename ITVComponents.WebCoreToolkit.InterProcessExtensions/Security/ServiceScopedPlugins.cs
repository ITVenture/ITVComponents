using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.InterProcessCommunication.Shared.Helpers;
using ITVComponents.InterProcessCommunication.Shared.Security;
using ITVComponents.WebCoreToolkit.WebPlugins;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.InterProcessExtensions.Security
{
    public class ServiceScopedPlugins:IFactoryWrapper
    {
        private IDictionary<string, ProxyWrapper> extendedProxies;

        public object this[string pluginName, IServiceProvider services] => FetchPlugin(pluginName, services);

        public bool Contains(string uniqueName, IServiceProvider services, out bool securityRequired)
        {
            if (extendedProxies.ContainsKey(uniqueName))
            {
                securityRequired = false;
                return true;
            }

            ResolveServices(services, out var selector, out var plugins);
            if(selector.GetPlugin(uniqueName) != null)
            {
                var tmp = plugins.GetFactory()[uniqueName, true];
                if (tmp != null)
                {
                    securityRequired = tmp.RequiresSecurity();
                    return true;
                }
            }

            securityRequired = false;
            return false;
        }

        public void AttachProxyDictionary(IDictionary<string, ProxyWrapper> proxies)
        {
            extendedProxies = proxies;
        }

        private object FetchPlugin(string pluginName, IServiceProvider services)
        {
            if (extendedProxies.TryGetValue(pluginName, out var ret))
            {
                return ret;
            }

            ResolveServices(services, out var selector, out var plugins);
            if (selector.GetPlugin(pluginName) != null)
            {
                return plugins.GetFactory()[pluginName, true];
            }

            return null;
        }

        private void ResolveServices(IServiceProvider services, out IWebPluginsSelector selector, out IWebPluginHelper plugins)
        {
            selector = services.GetService<IWebPluginsSelector>();
            plugins = services.GetService<IWebPluginHelper>();
        }
    }
}
