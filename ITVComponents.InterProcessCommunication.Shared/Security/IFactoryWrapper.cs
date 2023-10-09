using ITVComponents.InterProcessCommunication.Shared.Helpers;
using System;
using System.Collections;
using System.Collections.Generic;

namespace ITVComponents.InterProcessCommunication.Shared.Security
{
    public interface IFactoryWrapper
    {
        /// <summary>
        /// Gets a PluginInstance with the given name
        /// </summary>
        /// <param name="pluginName">the name of the desired plugin</param>
        /// <returns>the plugin-instance with the given name</returns>
        object this[string pluginName, IServiceProvider services] { get; }

        /// <summary>
        /// Gets a value indicating whether the specified plugin has been initialized 
        /// </summary>
        /// <param name="uniqueName">the uniquename for which to check in the list of initialized plugins</param>
        /// <param name="securityRequired">indicates whether the requested plugin requires security in order to be accessed</param>
        /// <returns>a value indicating whether the requested plugin is currently reachable</returns>
        bool Contains(string uniqueName, IServiceProvider services, out bool securityRequired);

        /// <summary>
        /// Attaches a dictionary containing extended proxy-objects that have been created and contain active elements that require to remain on the server instead of being serialized and sent to the client
        /// </summary>
        /// <param name="proxies">the dictionary containing proxy-objects</param>
        void AttachProxyDictionary(IDictionary<string, ProxyWrapper> proxies);

        string GetUniqueName(object plugin);
    }
}