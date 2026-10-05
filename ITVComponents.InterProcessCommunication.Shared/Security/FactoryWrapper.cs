using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Threading;
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

        private AsyncLocal<IPluginFactory> currentScope;

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
            currentScope = new AsyncLocal<IPluginFactory>();
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
                IPluginFactory scope = null;
                if (serviceDecorators.TryGetValue(pluginName, out var deco))
                {
                    retVal = deco;
                }
                else
                {
                    retVal = wrapped[pluginName];
                    if (retVal == null && currentScope.Value is { } activeScope)
                    {
                        // Nicht in der Hauptfabrik: dann kann es ein Bereichs-Plugin sein, das erst im Scope dieser
                        // Nachricht entsteht. Der Scope faellt fuer alles, was er nicht selbst kennt, auf die
                        // Hauptfabrik zurueck - fuer Singleton-Plugins aendert sich also nichts.
                        retVal = activeScope[pluginName, true];
                        if (retVal != null)
                        {
                            scope = activeScope;
                        }
                    }
                }

                if (retVal != null)
                {
                    if (retVal is not IServiceDecorator)
                    {
                        if (scope != null)
                        {
                            // Eine Instanz aus dem Scope lebt nur fuer diese Nachricht. Ihr Decorator entsteht im selben
                            // Scope und darf NICHT in den Cache: der lebt so lange wie der Wrapper, und der naechste
                            // Aufruf bekaeme den Decorator einer bereits freigegebenen Instanz.
                            var decorator = FindDecorator(scope, retVal) ?? FindDecorator(wrapped, retVal);
                            if (decorator != null)
                            {
                                retVal = decorator;
                            }
                        }
                        else
                        {
                            var decorator = FindDecorator(wrapped, retVal);
                            if (decorator != null)
                            {
                                serviceDecorators.TryAdd(pluginName, decorator);
                                retVal = decorator;
                            }
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
                    var decorator = FindDecorator(wrapped, plug);
                    if (decorator != null)
                    {
                        serviceDecorators.TryAdd(uniqueName, decorator);
                        plug = decorator;
                    }
                }

                securityRequired = plug.RequiresSecurity();
                retVal = hasSecurity || !securityRequired;
            }
            else if (currentScope.Value != null && wrapped.DescribeScopedPlugin(uniqueName) is { } scoped)
            {
                // Ein Bereichs-Plugin, das der Indexer im Scope dieser Nachricht bauen wuerde. Geprueft wird am
                // TYP, nicht an einer Instanz: eine Verfuegbarkeitsabfrage darf keinen Konstruktor laufen lassen,
                // der Geraete oder Verbindungen oeffnet. Ein Decorator ist so nicht zu sehen - er entstuende erst
                // im Scope; beim eigentlichen Zugriff prueft der Indexer die Sicherheit am echten Objekt erneut.
                securityRequired = SecurityExtensions.TypeRequiresSecurity(scoped.PluginType);
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

        /// <summary>
        /// Searches the given factory or scope for a decorator that wraps the provided service
        /// </summary>
        /// <param name="source">the factory or scope to search</param>
        /// <param name="service">the decorated service</param>
        /// <returns>the decorator of the service, or null if it is exposed directly</returns>
        private static IServiceDecorator FindDecorator(IEnumerable<IPlugin> source, object service)
        {
            return (IServiceDecorator)source.FirstOrDefault(t => t is IServiceDecorator d && d.DecoratedService == service);
        }

        public IPluginFactory OpenScope(Dictionary<string, object> dictionary, IServiceProvider services)
        {
            var retVal = wrapped.NewScope(dictionary, services, false);
            currentScope.Value = retVal;
            retVal.Disposed += (s, e) => currentScope.Value = null; 
            return retVal;
        }
    }
}