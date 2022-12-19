using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using ITVComponents.InterProcessCommunication.Shared.Helpers;
using ITVComponents.Plugins;

namespace ITVComponents.InterProcessCommunication.Shared.Security
{
    /// <summary>
    /// Wraps a PluginFactory and performs checks about security requriements of requested objects
    /// </summary>
    public class FactoryWrapper : IFactoryWrapper
    {
        /// <summary>
        /// holds the factory that holds all accessible objects
        /// </summary>
        private PluginFactory wrapped;

        /// <summary>
        /// Gets a value indicating whether security is provided in the underlaying channel
        /// </summary>
        private bool hasSecurity;

        private IDictionary<string, ProxyWrapper> extendedProxies;

        /// <summary>
        /// Holds a dictionary of extended Decorators that are used to save delicate Services from being exposed directly
        /// </summary>
        private ConcurrentDictionary<string, IServiceDecorator> serviceDecorators = new();

        /// <summary>
        /// Initializes a new instance of the FactoryWrapper class
        /// </summary>
        /// <param name="factory">the factory that is wrapped by this instance</param>
        /// <param name="extendedProxies">a dictionary containing all extended proxies that are currently used by the consuming remote proxy server</param>
        /// <param name="hasSecurity">indicates whether the underlaying channel supports security</param>
        internal FactoryWrapper(PluginFactory factory, bool hasSecurity)
        {
            wrapped = factory;
            this.hasSecurity = hasSecurity;
        }

        /// <summary>
        /// Gets a PluginInstance with the given name
        /// </summary>
        /// <param name="pluginName">the name of the desired plugin</param>
        /// <returns>the plugin-instance with the given name</returns>
        public object this[string pluginName, IServiceProvider services]
        {
            get
            {
                object retVal = null;
                if (serviceDecorators.TryGetValue(pluginName, out var deco))
                {
                    retVal = deco;
                }
                else
                {
                    retVal = wrapped[pluginName];
                }

                if (retVal != null)
                {
                    if (retVal is not IServiceDecorator)
                    {
                        var decorator =
                            wrapped.FirstOrDefault(t => t is IServiceDecorator d && d.DecoratedService == retVal);
                        if (decorator != null)
                        {
                            serviceDecorators.TryAdd(pluginName, (IServiceDecorator)decorator);
                            retVal = decorator;
                        }
                    }

                    IPlugin plug = (IPlugin) retVal;
                    if (!hasSecurity && plug.RequiresSecurity())
                    {
                        throw new InterProcessException("Unable to use an Object that requires Security in an Unsecured Channel!", null);
                    }
                }
                else
                {
                    if (extendedProxies.ContainsKey(pluginName))
                    {
                        retVal = extendedProxies[pluginName].Value;
                    }
                }

                return retVal;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the specified plugin has been initialized 
        /// </summary>
        /// <param name="uniqueName">the uniquename for which to check in the list of initialized plugins</param>
        /// <param name="securityRequired">indicates whether the requested plugin requires security in order to be accessed</param>
        /// <returns>a value indicating whether the requested plugin is currently reachable</returns>
        public bool Contains(string uniqueName, IServiceProvider services, out bool securityRequired)
        {

            bool retVal = wrapped.Contains(uniqueName) || serviceDecorators.ContainsKey(uniqueName);
            securityRequired = false;
            if (retVal)
            {
                IPlugin plug = null;
                if (serviceDecorators.TryGetValue(uniqueName, out var deco))
                {
                    plug = deco;
                }
                else
                {
                    plug = wrapped[uniqueName];
                }

                if (plug is not IServiceDecorator)
                {
                    var decorator =
                        wrapped.FirstOrDefault(t => t is IServiceDecorator d && d.DecoratedService == plug);
                    if (decorator != null)
                    {
                        serviceDecorators.TryAdd(uniqueName, (IServiceDecorator)decorator);
                        plug = decorator;
                    }
                }

                securityRequired = plug.RequiresSecurity();
                retVal = hasSecurity || !securityRequired;
            }

            if (!retVal)
            {
                retVal = extendedProxies.ContainsKey(uniqueName);
            }

            return retVal;
        }

        /// <summary>
        /// Attaches a dictionary containing extended proxy-objects that have been created and contain active elements that require to remain on the server instead of being serialized and sent to the client
        /// </summary>
        /// <param name="proxies">the dictionary containing proxy-objects</param>
        public void AttachProxyDictionary(IDictionary<string, ProxyWrapper> proxies)
        {
            extendedProxies = proxies;
        }
    }
}