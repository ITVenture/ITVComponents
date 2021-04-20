namespace ITVComponents.InterProcessCommunication.Shared.Security
{
    public interface IFactoryWrapper
    {
        /// <summary>
        /// Gets a PluginInstance with the given name
        /// </summary>
        /// <param name="pluginName">the name of the desired plugin</param>
        /// <returns>the plugin-instance with the given name</returns>
        object this[string pluginName] { get; }

        /// <summary>
        /// Gets a value indicating whether the specified plugin has been initialized 
        /// </summary>
        /// <param name="uniqueName">the uniquename for which to check in the list of initialized plugins</param>
        /// <param name="securityRequired">indicates whether the requested plugin requires security in order to be accessed</param>
        /// <returns>a value indicating whether the requested plugin is currently reachable</returns>
        bool Contains(string uniqueName, out bool securityRequired);
    }
}