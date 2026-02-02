using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.InterProcessCommunication.Shared.Helpers;
using ITVComponents.Plugins;
using ITVComponents.Plugins.PluginServices;

namespace ITVComponents.InterProcessCommunication.Shared.Security.SpecialWrappers
{
    internal class DictionaryWrapper:IFactoryWrapper, IPluginFactory
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

        public IPluginFactory OpenScope(Dictionary<string, object> dictionary, IServiceProvider services)
        {
            return this;
        }

        public void Dispose()
        {
            OnDisposed();
        }

        public IEnumerator<IPlugin> GetEnumerator()
        {
            return exposedObjects.Where(n => n.Value is IPlugin).Select(n => (IPlugin)n.Value).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IPlugin this[string pluginName] => exposedObjects[pluginName] as IPlugin;

        public IPlugin this[string pluginName, bool triggerAsParameterRequest, PluginRef callingPluginRef] => exposedObjects[pluginName] as IPlugin;

        public IPlugin this[string pluginName, bool triggerAsParameterRequest] => exposedObjects[pluginName] as IPlugin;

        public T LoadPlugin<T>(string uniqueName, string pluginConstructor) where T : class, IPlugin
        {
            return exposedObjects[uniqueName] as T;
        }

        public T LoadPlugin<T>(string uniqueName, string pluginConstructor, Dictionary<string, object> customVariables, bool? doBuffer = null) where T : class, IPlugin
        {
            return exposedObjects[uniqueName] as T;
        }

        public T LoadPlugin<T>(string uniqueName, string pluginConstructor, bool buffer) where T : class, IPlugin
        {
            return exposedObjects[uniqueName] as T;
        }

        public IEnumerable<T> GetPlugins<T>() where T : class, IPlugin
        {
            return exposedObjects.Where(n => n.Value is T).Select(n => (T)n.Value);
        }

        public T GetPlugin<T>() where T : class, IPlugin
        {
            return GetPlugins<T>().FirstOrDefault();
        }

        public IPlugin[] ScopeClose()
        {
            return Array.Empty<IPlugin>();
        }

        public event EventHandler Disposed;

        protected virtual void OnDisposed()
        {
            Disposed?.Invoke(this, EventArgs.Empty);
        }
    }
}
