using ITVComponents.Serialization;

namespace ITVComponents.Plugins.RuntimeSerialization
{
    /// <summary>
    /// Enables an application using a PluginFactory to automatically save the runtime status of all plugins after shutting it down
    /// </summary>
    public interface IRuntimeSerializer : IPlugin
    {
        /// <summary>
        /// Saves the runtime information provided into a file
        /// </summary>
        /// <param name="information">the Runtime information that needs to be saved into a file</param>
        void SaveRuntimeStatus(RuntimeInformation information);

        /// <summary>
        /// Loads the runtime status of the application from a file
        /// </summary>
        /// <returns>the Runtime status of the current application's plugins</returns>
        RuntimeInformation LoadRuntimeStatus();
    }
}