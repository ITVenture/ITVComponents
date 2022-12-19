using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.InterProcessCommunication.Shared.Helpers;

namespace ITVComponents.InterProcessCommunication.Shared.Security.SpecialWrappers
{
    internal class DictionaryWrapper:IFactoryWrapper
    {
        private readonly IDictionary<string, object> exposedObjects;
        private IDictionary<string, ProxyWrapper> extendedProxies;
        private readonly bool hasSecurity;

        internal DictionaryWrapper(IDictionary<string,object> exposedObjects, bool hasSecurity)
        {
            this.exposedObjects = exposedObjects;
            this.hasSecurity = hasSecurity;
        }

        /// <summary>
        /// Gets a PluginInstance with the given name
        /// </summary>
        /// <param name="pluginName">the name of the desired plugin</param>
        /// <returns>the plugin-instance with the given name</returns>
        public object this[string pluginName, IServiceProvider services] {
            get
            {
                if (exposedObjects.ContainsKey(pluginName))
                {
                    object retVal = exposedObjects[pluginName];
                    if (retVal != null)
                    {
                        
                        if (!hasSecurity && retVal.RequiresSecurity())
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

                return null;
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
            bool retVal = exposedObjects.ContainsKey(uniqueName);
            securityRequired = false;
            if (retVal)
            {
                securityRequired = exposedObjects[uniqueName].RequiresSecurity();
                retVal = hasSecurity || !securityRequired;
            }

            if (!retVal)
            {
                retVal = extendedProxies.ContainsKey(uniqueName);
            }

            return retVal;
        }

        public void AttachProxyDictionary(IDictionary<string, ProxyWrapper> proxies)
        {
            extendedProxies = proxies;
        }
    }
}
