using System;
using System.Collections.Generic;
using ITVComponents.Plugins.PluginServices;

namespace ITVComponents.Plugins;

public interface IPluginFactory: IDisposable, IEnumerable<IPlugin>
{
    /// <summary>
    /// Gets a PluginInstance with the given name
    /// </summary>
    /// <param name="pluginName">the name of the desired plugin</param>
    /// <returns>the plugin-instance with the given name</returns>
    IPlugin this[string pluginName] { get; }

    /// <summary>
    /// Gets a PluginInstance with the given name
    /// </summary>
    /// <param name="pluginName">the name of the desired plugin</param>
    /// <param name="triggerAsParameterRequest">indicates whether to request the plugin as unknownparameter if it's missing in the list of loaded plugins</param>
    /// <returns>the plugin-instance with the given name</returns>
    IPlugin this[string pluginName, bool triggerAsParameterRequest, PluginRef callingPluginRef] { get; }

    /// <summary>
    /// Gets a PluginInstance with the given name
    /// </summary>
    /// <param name="pluginName">the name of the desired plugin</param>
    /// <param name="triggerAsParameterRequest">indicates whether to request the plugin as unknownparameter if it's missing in the list of loaded plugins</param>
    /// <returns>the plugin-instance with the given name</returns>
    IPlugin this[string pluginName, bool triggerAsParameterRequest] { get; }

    /// <summary>
    /// Creates a new plugin and uses the default buffering mode
    /// </summary>
    /// <typeparam name="T">the Type that is supposed to be created</typeparam>
    /// <param name="uniqueName">the unique name for the created plugin</param>
    /// <param name="pluginConstructor">Constructorstring for the Plugin in the Format [AssemblyPath]&lt;FullQulifiedType&gt;Parameters</param>
    /// <returns>the created IPlugin instance</returns>
    T LoadPlugin<T>(string uniqueName, string pluginConstructor) where T : class, IPlugin;

    T LoadPlugin<T>(string uniqueName, string pluginConstructor, Dictionary<string,object> customVariables, bool? doBuffer = null) where T : class, IPlugin;

    /// <summary>
    /// Creates a new Plugin
    /// </summary>
    /// <param name="uniqueName">the unique name for the created plugin</param>
    /// <param name="pluginConstructor">Constructorstring for the Plugin in the Format [AssemblyPath]&lt;FullQulifiedType&gt;Parameters</param>
    /// <param name="buffer">indicates whether to keept the generated object for controlled disposal</param>
    /// <typeparam name="T">the Type that is supposed to be created</typeparam>
    /// <returns>the created IPlugin instance</returns>
    T LoadPlugin<T>(string uniqueName, string pluginConstructor, bool buffer) where T : class, IPlugin;

    /// <summary>
    /// Gets all loaded plugins of the specified type or interface
    /// </summary>
    /// <typeparam name="T">the desired plugin type</typeparam>
    /// <returns>an enumerable that contains all matching plugins</returns>
    IEnumerable<T> GetPlugins<T>() where T : class, IPlugin;

    /// <summary>
    /// Gets the first Plugin that implements the specified type or interface
    /// </summary>
    /// <typeparam name="T">the desired plugin type</typeparam>
    /// <returns>the first occurrence of the specified plugin-Type or null if none was found</returns>
    T GetPlugin<T>() where T: class, IPlugin;

    public IPlugin[] ScopeClose();

    event EventHandler Disposed;
}