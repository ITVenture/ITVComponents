using System;
using System.Collections.Generic;
using System.Reflection;
using ITVComponents.Plugins.Config;
using ITVComponents.Plugins.PluginServices;

namespace ITVComponents.Plugins;

public interface IPluginFactory: IDelayedDisposable//, IEnumerable<object>
{
    /// <summary>
    /// Gets or sets a value indicating whether to allow plugins to request this factory object by having a constructor parameter called $factory
    /// </summary>
    bool AllowFactoryParameter { get; set; }

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
    /// <returns>a value indicating whether the requested plugin is currently reachable</returns>
    bool Contains(string uniqueName);

    /*/// <summary>
    /// Creates a new plugin and uses the default buffering mode
    /// </summary>
    /// <typeparam name="T">the Type that is supposed to be created</typeparam>
    /// <param name="uniqueName">the unique name for the created plugin</param>
    /// <param name="pluginConstructor">Constructorstring for the Plugin in the Format [AssemblyPath]&lt;FullQulifiedType&gt;Parameters</param>
    /// <returns>the created IPlugin instance</returns>
    T LoadPlugin<T>(string uniqueName, string pluginConstructor) where T : class, IPlugin;*/

    T LoadPlugin<T>(string uniqueName, string pluginConstructor, Dictionary<string,object> customVariables) where T : class;

    /// <summary>
    /// Indicates whether the given UniqueName is configured and loadable in the current state of initialization
    /// </summary>
    /// <param name="uniqueName">the uniqueName of the demanded Plugin</param>
    /// <param name="callerInfo">a caller-info identifying a plugin that is currently being initialized and requests the specified plugin</param>
    /// <param name="pluginInstance">when returning true, that pluginInstance contains the loaded plugin instance</param>
    /// <returns>a value indicating whether the given Plugin is configured and loadable</returns>
    public bool IsKnownPlugin(string uniqueName, PluginRef callerInfo, out object pluginInstance);

    /// <summary>
    /// Gets a value indicating whether is already initialized
    /// </summary>
    /// <param name="uniqueName">the uniqueName of the plugin for which to check in the collection of loaded Plugins</param>
    /// <param name="pluginInstance">when already initialized, the instance is set with the instance that was found</param>
    /// <returns>a value indicating whether the plugin is available</returns>
    public bool IsPluginLoaded(string uniqueName, out object pluginInstance);

    /*/// <summary>
    /// Creates a new Plugin
    /// </summary>
    /// <param name="uniqueName">the unique name for the created plugin</param>
    /// <param name="pluginConstructor">Constructorstring for the Plugin in the Format [AssemblyPath]&lt;FullQulifiedType&gt;Parameters</param>
    /// <param name="buffer">indicates whether to keept the generated object for controlled disposal</param>
    /// <typeparam name="T">the Type that is supposed to be created</typeparam>
    /// <returns>the created IPlugin instance</returns>
    T LoadPlugin<T>(string uniqueName, string pluginConstructor, bool buffer) where T : class, IPlugin;*/

    /// <summary>
    /// Verifies a given constructor and returns a boolean value indicating whether the plugin-string is processable in a running environment
    /// </summary>
    /// <param name="uniqueName">the uniquename of the plugin</param>
    /// <param name="constructor">the constructor that is used to load a plugin</param>
    /// <param name="buffer">indicates whether to buffer loaded plugins</param>
    /// <returns>a value indicating whether the plugin-test was successful</returns>
    bool VerifyConstructor(string uniqueName, string constructor, bool? buffer = null);

    /// <summary>
    /// Registers an assembly for being used as plugin source
    /// </summary>
    /// <param name="assemblyName">the accessible name of the assembly</param>
    /// <param name="targetAssembly">the object representation of the assembly</param>
    void RegisterAssembly(string assemblyName, Assembly targetAssembly);

    /// <summary>
    /// Initializes the known plugins in the order they were initialized
    /// </summary>
    void InitializeDeferrables();

    /*/// <summary>
    /// Initializes the known plugins in the order they were initialized
    /// </summary>
    /// <param name="orderedNames">the names of all known plugins</param>
    void InitializeDeferrables(string[] orderedNames);*/

    /// <summary>
    /// Registers an object that can be resolved when a creating object requests a constructor parameter
    /// </summary>
    /// <param name="parameterName">the name of the used constructor parameter</param>
    /// <param name="parameterInstance">the value that can be accessed used the provided parametername</param>
    void RegisterObject(string parameterName, object parameterInstance);

    /// <summary>
    /// Registers a Type for PluginTests that can be resolved when a Plugin-constructor requires a specific object-name
    /// </summary>
    /// <param name="parameterName">the name of the tester-object</param>
    /// <param name="targetType">the type of the tester-object. This will be converted to a Reflection-Only - Type</param>
    void RegisterObjectType(string parameterName, Type targetType);

    /// <summary>
    /// Registers a Type for PluginTests in the local thread that can be resolved when a Plugin-constructor requires a specific object-name
    /// </summary>
    /// <param name="parameterName">the name of the tester-object</param>
    /// <param name="targetType">the type of the tester-object. This will be converted to a Reflection-Only - Type</param>
    void RegisterObjectTypeLocal(string parameterName, Type targetType);

    /// <summary>
    /// Registers an object for Parameter-callbacks only for the current thread
    /// </summary>
    /// <param name="parameterName">the parameter for which to register an instance</param>
    /// <param name="parameterInstance">the value to return if the factory requests the given parameter in the local thread</param>
    void RegisterObjectLocal(string parameterName, object parameterInstance);

    /// <summary>
    /// Clears all local Object-registrations
    /// </summary>
    void ClearLocalRegistrations();

    /*/// <summary>
    /// Gets a value indicating whether the provided parameter is konwn by the factory
    /// </summary>
    /// <param name="parameterName">the parameter name for which to check in this factory</param>
    /// <returns>a value indicating whether the given object is known</returns>
    bool IsObjectRegistered(string parameterName);*/

    /*/// <summary>
    /// Gets a registered obejct from this factory
    /// </summary>
    /// <param name="parameterName">the parameter that was previously registered in this factory</param>
    /// <returns>the previously registered object</returns>
    object GetRegisteredObject(string parameterName);*/

    public string GetUniqueName(object plugin);

    /// <summary>
    /// Handles the event when the Constructor caller detects an unkown constructor parameter
    /// </summary>
    event UnknownConstructorParameterEventHandler UnknownConstructorParameter;

    /// <summary>
    /// Provides an event informing a caller about the initialization of a new plugin instance
    /// </summary>
    event PluginInitializedEventHandler PluginInitialized;

    /// <summary>
    /// Is raised when a Plugin that is implemented as generic type is constructed.
    /// </summary>
    event ImplementGenericTypeEventHandler ImplementGenericType;

    /// <summary>
    /// Is raised when this PluginFactory is disposed 
    /// </summary>
    event EventHandler Disposed;

    void Start();

    /// <summary>
    /// Filters the current Factory and its parent instances with the given filter for objects
    /// </summary>
    /// <typeparam name="T">the Type-filter to pre-filter the plugins</typeparam>
    /// <param name="filter">the filter-action to apply on any object matching the type-filter</param>
    /// <returns>an IEnumerable containing all matching elements</returns>
    IEnumerable<T> FilterPlugins<T>(Func<T, bool> filter) where T : class;
}