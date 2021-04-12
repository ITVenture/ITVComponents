using System.Collections.Generic;
using System.ServiceModel;
using ITVComponents.InterProcessCommunication.Shared.Helpers;
using ITVComponents.Plugins;

namespace ITVComponents.InterProcessCommunication.Shared.Security
{
    /// <summary>
    /// Wraps a PluginFactory and performs checks about security requriements of requested objects
    /// </summary>
    public class FactoryWrapper
    {
        /// <summary>
        /// holds the factory that holds all accessible objects
        /// </summary>
        private PluginFactory wrapped;

        /// <summary>
        /// Gets a value indicating whether security is provided in the underlaying channel
        /// </summary>
        private bool hasSecurity;

        private Dictionary<string, ProxyWrapper> extendedProxies;

        /// <summary>
        /// Initializes a new instance of the FactoryWrapper class
        /// </summary>
        /// <param name="factory">the factory that is wrapped by this instance</param>
        /// <param name="extendedProxies">a dictionary containing all extended proxies that are currently used by the consuming remote proxy server</param>
        /// <param name="hasSecurity">indicates whether the underlaying channel supports security</param>
        internal FactoryWrapper(PluginFactory factory, Dictionary<string,ProxyWrapper> extendedProxies, bool hasSecurity)
        {
            wrapped = factory;
            this.hasSecurity = hasSecurity;
            this.extendedProxies = extendedProxies;
        }

        /// <summary>
        /// Gets a PluginInstance with the given name
        /// </summary>
        /// <param name="pluginName">the name of the desired plugin</param>
        /// <returns>the plugin-instance with the given name</returns>
        public object this[string pluginName]
        {
            get
            {
                object retVal = wrapped[pluginName];
                if (retVal != null)
                {
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
        public bool Contains(string uniqueName, out bool securityRequired)
        {

            bool retVal = wrapped.Contains(uniqueName);
            securityRequired = false;
            if (retVal)
            {
                securityRequired = wrapped[uniqueName].RequiresSecurity();
                retVal = hasSecurity || !securityRequired;
            }

            if (!retVal)
            {
                retVal = extendedProxies.ContainsKey(uniqueName);
            }

            return retVal;
        }
    }
}