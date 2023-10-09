//-----------------------------------------------------------------------
// <copyright file="PluginFactory.cs" company="IT-Venture GmbH">
//     2009 by IT-Venture GmbH
// </copyright>
//-----------------------------------------------------------------------
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.RegularExpressions;
using System.Threading;
using System.Transactions;
using Antlr4.Runtime.Misc;
using ITVComponents.AssemblyResolving;
using ITVComponents.Helpers;
using ITVComponents.Logging;
using ITVComponents.Plugins.Config;
using ITVComponents.Plugins.DIIntegration;
using ITVComponents.Plugins.Initialization;
using ITVComponents.Plugins.Model;
using ITVComponents.Plugins.PluginServices;
using ITVComponents.Plugins.Resources;
using ITVComponents.Plugins.RuntimeSerialization;
using ITVComponents.Plugins.SelfRegistration;
using ITVComponents.Plugins.SingletonPattern;
using ITVComponents.Scripting.CScript.Core;
using ITVComponents.Scripting.CScript.Core.Methods;
using ITVComponents.Scripting.CScript.Helpers;
using ITVComponents.Serialization;
using ITVComponents.Settings;
using ITVComponents.Threading;
using Newtonsoft.Json.Linq;

namespace ITVComponents.Plugins
{
    /// <summary>
    /// Creates Log - Adapters and passes Messages through.
    /// </summary>
    public class PluginFactory : ICriticalComponent, ISingletonFactory
    {
        /// <summary>
        /// All Plugins that are registered in this instance
        /// </summary>
        private ConcurrentDictionary<string, object> plugins;

        /// <summary>
        /// Holds a list of UniqueNames that is filled in the order of the initialization
        /// </summary>
        private List<string> orderedNames = new List<string>();

        /// <summary>
        /// Used to make sure that plugins are not being tried to load concurrently
        /// </summary>
        private ConcurrentDictionary<string, ManualResetEventSlim> pluginInitializationPromises;

        /*/// <summary>
        /// All objects that can be accessed directly as constructor parameters when an object requests it
        /// </summary>
        private ConcurrentDictionary<string, object> registeredObjects;*/


        private AsyncLocal<Dictionary<string, object>> localRegistrations;

        /*/// <summary>
        /// a list of all loaded runtime serializers for this factory
        /// </summary>
        private IRuntimeSerializer runtimeSerializer;*/

        /// <summary>
        /// indicates whether this instance has been disposed
        /// </summary>
        private bool disposed = false;

        /// <summary>
        /// A List of Registered Direcories that is browsed on AssemblyLoad event
        /// </summary>
        private List<string> registeredDirectories;

        /// <summary>
        /// A list of registered assemblies used for loading plugins from dynamic assemblies
        /// </summary>
        private Dictionary<string, Assembly> registeredAssemblies;

        /// <summary>
        /// Indicates whether the class is currently trying to dispose
        /// </summary>
        private bool disposing = false;

        /// <summary>
        /// Disposer thread that is used to run the dispose method
        /// </summary>
        private Thread disposer;

        /// <summary>
        /// Manual reset event that is set when the dispose call has finished
        /// </summary>
        private ManualResetEvent waitForDisposedEvent;

        /// <summary>
        /// object to synchronize the usage of resources
        /// </summary>
        private object threadLock = new object();

        /*/// <summary>
        /// Holds the latest runtime status of all plugins providing it
        /// </summary>
        private RuntimeInformation runtimeStatus = new RuntimeInformation();*/

        /// <summary>
        /// Indicates whether this factory is the singleton factory
        /// </summary>
        //private bool singletonFactory = false;

        /// <summary>
        /// Indicates whether to allow this factory to return itself when a plugin requests the parameter $factory
        /// </summary>
        private bool allowFactoryParameter = false;

        /// <summary>
        /// defines the current startupPhase
        /// </summary>
        private PluginInitializationPhase currentPhase;

        /// <summary>
        /// indicates whether the current phase was completed
        /// </summary>
        private bool phaseCompleted = false;

        /// <summary>
        /// Indicates whether the PluginFactory will put all IConfigurableComponent Plugins into a mode that will allow configuring wihtout having anything running
        /// </summary>
        private bool configurationOnly = false;

        /// <summary>
        /// Holds an instance of an object that is capable for formatting string literals before a constructor is invoked
        /// </summary>
        private IStringFormatProvider stringLiteralFormatter;

        /*/// <summary>
        /// a list of dynamically loaded plugins
        /// </summary>
        private string[] dynamicPlugIns;*/

        private IPluginFactory parent;

        /// <summary>
        /// Initializes static members of the pluginFactory class
        /// </summary>
        static PluginFactory()
        {
            AssemblyResolver.Enabled = true;
        }

        /// <summary>
        /// Initializes a new instance of the PluginFactory class
        /// </summary>
        public PluginFactory() : this(PluginInitializationPhase.Startup, false)
        {
        }

        /// <summary>
        /// Initializes a new instance of the PluginFactory class
        /// </summary>
        /// <param name="startPhase">the initial phase of this pluginFactory </param>
        /// <param name="configurationOnly">indicates whether the loaded plugins are only required for loading and should not be initialized</param>
        public PluginFactory(PluginInitializationPhase startPhase, bool configurationOnly) : this(configurationOnly)
        {
            this.currentPhase = startPhase;
        }

        public PluginFactory(PluginInitializationPhase startPhase, bool configurationOnly, params IDynamicLoader[] pluginLoaders)
            : this(startPhase, configurationOnly)
        {
            int lid = 0;
            foreach (var loader in pluginLoaders)
            {
                plugins.TryAdd($"@DefaultLoader{++lid}", loader);
            }
        }

        public PluginFactory(PluginInitializationPhase startPhase, bool configurationOnly, IServiceProvider services,
            params IDynamicLoader[] pluginLoaders) : this(startPhase, configurationOnly, pluginLoaders)
        {
            plugins.TryAdd("services", services);
        }

        public PluginFactory(PluginInitializationPhase startPhase, bool configurationOnly, IServiceProvider services,IPluginFactory parent,
            params IDynamicLoader[] pluginLoaders) : this(startPhase, configurationOnly, services, pluginLoaders)
        {
            this.parent = parent;
        }

        /*/// <summary>
        /// Initializes a new instance of the PluginFactory class
        /// </summary>
        /// <param name="buffer">indicates whether to buffer loaded plugins</param>
        /// <param name="reflectionFactory">indicates whether to use this factory as pluginstring verifyer only</param>
        /// <param name="deferredInitialization">indicates whether to use deferred initialization for the loaded plugins</param>
        /// <param name="configurationOnly">indicates whether the configurable plugins should not be initialized in order to perform configuration tasks on these components</param>
        public PluginFactory(bool buffer, bool reflectionFactory, bool deferredInitialization, bool configurationOnly) : this(buffer, false,
            reflectionFactory, deferredInitialization, configurationOnly)
        {
        }*/

        /// <summary>
        /// Initializes a new instance of the PluginFactory class
        /// </summary>
        /// <param name="buffer">indicates whether to buffer generated objects for later use</param>
       // /// <param name="singletonFactory">indicates whether to use this instance as the singleton plugin instance</param>
        /// <param name="deferredInitialization">indicates whether to use deferred initialization for the loaded plugins</param>
        /// <param name="configurationOnly">indicates whether the configurable plugins should not be initialized in order to perform configuration tasks on these components</param>
        internal PluginFactory(bool configurationOnly)
        {
            //this.singletonFactory = singletonFactory;
            this.configurationOnly = configurationOnly;
            /*if (!singletonFactory && !reflectionFactory)
            {
                SingletonEnvironment.FactoryInitializing();
            }*/

            registeredDirectories = new List<string>();
            registeredDirectories.Add(Path.GetDirectoryName(Assembly.GetCallingAssembly().Location));
            registeredAssemblies = new Dictionary<string, Assembly>();
            this.plugins = new ConcurrentDictionary<string, object>();
            this.pluginInitializationPromises = new ConcurrentDictionary<string, ManualResetEventSlim>();
            //registeredObjects = new ConcurrentDictionary<string, object>();
            localRegistrations = new AsyncLocal<Dictionary<string, object>>();//() => new Dictionary<string, object>());
            disposer = new Thread(Dispose);
            waitForDisposedEvent = new ManualResetEvent(false);
        }

        private bool DeferredStartup => (currentPhase == PluginInitializationPhase.Startup && !phaseCompleted) ||
                                        currentPhase == PluginInitializationPhase.SingletonStatic ||
                                        currentPhase == PluginInitializationPhase.ScopeStatic;

        /// <summary>
        /// Gets a PluginInstance with the given name
        /// </summary>
        /// <param name="pluginName">the name of the desired plugin</param>
        /// <returns>the plugin-instance with the given name</returns>
        public object this[string pluginName]
        {
            get
            {
                return GetObjectByName(pluginName, null);
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether to allow plugins to request this factory object by having a constructor parameter called $factory
        /// </summary>
        public bool AllowFactoryParameter { get { return allowFactoryParameter; } set { allowFactoryParameter = value; } }

        private IDynamicLoader[] DynamicLoaders =>
            (from t in plugins where t.Value is IDynamicLoader select (IDynamicLoader)t.Value).ToArray();

        /// <summary>
        /// Gets a value indicating whether the specified plugin has been initialized 
        /// </summary>
        /// <param name="uniqueName">the uniquename for which to check in the list of initialized plugins</param>
        /// <returns>a value indicating whether the requested plugin is currently reachable</returns>
        public bool Contains(string uniqueName)
        {
            return plugins.ContainsKey(uniqueName);
        }

        /*/// <summary>
        /// Creates a new plugin and uses the default buffering mode
        /// </summary>
        /// <typeparam name="T">the Type that is supposed to be created</typeparam>
        /// <param name="uniqueName">the unique name for the created plugin</param>
        /// <param name="pluginConstructor">Constructorstring for the Plugin in the Format [AssemblyPath]&lt;FullQulifiedType&gt;Parameters</param>
        /// <returns>the created IPlugin instance</returns>
        public T LoadPlugin<T>(string uniqueName, string pluginConstructor) where T : class, IPlugin
        {
            return LoadPlugin<T>(uniqueName, pluginConstructor, buffer);
        }*/

        public IEnumerable<T> FilterPlugins<T>(Func<T, bool> filter) where T : class
        {
            if (parent != null)
            {
                foreach (var t in parent.FilterPlugins<T>(filter))
                {
                    yield return t;
                }
            }

            foreach (var t in plugins.OfType<T>())
            {
                if (filter(t))
                {
                    yield return t;
                }
            }
        }

        public T LoadPlugin<T>(string uniqueName, string pluginConstructor, Dictionary<string,object> customVariables) where T : class
        {
            if (disposing || disposed)
            {
                return null;
            }

            try
            {
                object retVal;
                if (TryLoadPlugin(uniqueName, pluginConstructor, true, customVariables, out retVal))
                {
                    if (retVal is IDeferredInit init)
                    {
                        if (!(configurationOnly && retVal is IConfigurableComponent))
                        {
                            if (!DeferredStartup || init.ForceImmediateInitialization)
                            {
                                if (!init.Initialized)
                                {
                                    init.Initialize();
                                }
                            }
                        }
                    }

                    if (retVal is IConfigurableComponent cfgComponent)
                    {
                        if (!(retVal is IConfigurablePlugin))
                        {
                            LogEnvironment.LogDebugEvent($"You are initializing a component that implements IConfigurableComponent, but is not an IConfigurablePlugin. Consider implementing the IConfigurablePlugin for better support. PluginName: {uniqueName}", LogSeverity.Warning);
                        }

                        JsonSettings.RegisterSettingsConsumer(cfgComponent);
                        if (retVal is IConfigurablePlugin cfgPlugin)
                        {
                            cfgPlugin.ReadSettings();
                        }
                    }

                    if (retVal is IStringFormatProvider prov)
                    {
                        if (stringLiteralFormatter != null)
                        {
                            LogEnvironment.LogDebugEvent($"There already is an instance loaded for String-formatting ({GetUniqueName(stringLiteralFormatter)}). This instance ({uniqueName}) is being ignored.", LogSeverity.Warning);
                            return (T)retVal;
                        }

                        stringLiteralFormatter = prov;
                    }

                    return (T)retVal;
                }
            }
            catch (Exception ex)
            {
                LogEnvironment.LogDebugEvent(null, ex.OutlineException(), (int)LogSeverity.Error, "PluginSystem");
                throw;
            }

            LogEnvironment.LogDebugEvent(null, "Failed to load plugin", (int)LogSeverity.Error, "PluginSystem");
            throw new Exception("Failed to load plugin");
        }

        public string GetUniqueName(object plugin)
        {
            var tmp = plugins.ToArray();
            if (tmp.Any(n => n.Value == plugin))
            {
                return tmp.First(n => n.Value == plugin).Key;
            }

            return null;
        }

        /*/// <summary>
        /// Creates a new Plugin
        /// </summary>
        /// <param name="uniqueName">the unique name for the created plugin</param>
        /// <param name="pluginConstructor">Constructorstring for the Plugin in the Format [AssemblyPath]&lt;FullQulifiedType&gt;Parameters</param>
        /// <param name="buffer">indicates whether to keept the generated object for controlled disposal</param>
        /// <typeparam name="T">the Type that is supposed to be created</typeparam>
        /// <returns>the created IPlugin instance</returns>
        public T LoadPlugin<T>(string uniqueName, string pluginConstructor, bool buffer) where T : class, IPlugin
        {
            return LoadPlugin<T>(uniqueName, pluginConstructor, null, buffer);
        }*/

        /// <summary>
        /// Verifies a given constructor and returns a boolean value indicating whether the plugin-string is processable in a running environment
        /// </summary>
        /// <param name="uniqueName">the uniquename of the plugin</param>
        /// <param name="constructor">the constructor that is used to load a plugin</param>
        /// <param name="buffer">indicates whether to buffer loaded plugins</param>
        /// <returns>a value indicating whether the plugin-test was successful</returns>
        public bool VerifyConstructor(string uniqueName, string constructor, bool? buffer = null)
        {
            throw new InvalidOperationException("Not available on default PluginFactory");
            //object dummy;
            //return TryLoadPlugin(uniqueName, constructor, buffer ?? this.buffer, null, out dummy);
        }

        /// <summary>
        /// Registers an assembly for being used as plugin source
        /// </summary>
        /// <param name="assemblyName">the accessible name of the assembly</param>
        /// <param name="targetAssembly">the object representation of the assembly</param>
        public void RegisterAssembly(string assemblyName, Assembly targetAssembly)
        {
            if (!registeredAssemblies.ContainsKey(assemblyName))
            {
                registeredAssemblies.Add(assemblyName, targetAssembly);
            }
            else
            {
                throw new Exception(Messages.AssemblyNameAlreadyRegisteredError);
            }
        }

        /// <summary>
        /// Initializes the known plugins in the order they were initialized
        /// </summary>
        public void InitializeDeferrables()
        {

            string[] names;
            lock (orderedNames)
            {
                names = orderedNames.ToArray();
                orderedNames.Clear();
            }

            InitializeDeferrables(names);
        }

        /// <summary>
        /// Tries to dispose the object and waits for the provided timeout
        /// </summary>
        /// <param name="timeout">the timeout to wait for the object to dispose</param>
        /// <returns>a value indicating whether the object could be disposed</returns>
        public bool Dispose(int timeout)
        {
            bool retVal = disposed;
            bool isDisposing = false;
            if (!retVal)
            {
                lock (threadLock)
                {
                    if (disposing && disposer.ThreadState != ThreadState.Running)
                    {
                        isDisposing = true;
                    }
                    if (!disposing)
                    {
                        disposer.Start();
                    }
                }

                if (!isDisposing)
                {
                    retVal = disposer.Join(timeout);
                }
                else
                {
                    retVal = waitForDisposedEvent.WaitOne(timeout);
                }
            }

            return retVal;
        }

        /// <summary>
        /// Registers an object that can be resolved when a creating object requests a constructor parameter
        /// </summary>
        /// <param name="parameterName">the name of the used constructor parameter</param>
        /// <param name="parameterInstance">the value that can be accessed used the provided parametername</param>
        public void RegisterObject(string parameterName, object parameterInstance)
        {
            plugins.TryAdd(parameterName,
                    parameterInstance);
        }

        /// <summary>
        /// Registers a Type for PluginTests that can be resolved when a Plugin-constructor requires a specific object-name
        /// </summary>
        /// <param name="parameterName">the name of the tester-object</param>
        /// <param name="targetType">the type of the tester-object. This will be converted to a Reflection-Only - Type</param>
        public void RegisterObjectType(string parameterName, Type targetType)
        {
            if (currentPhase == PluginInitializationPhase.Startup)
            {
                throw new InvalidOperationException("Supported only after DependencyInjection was initialized!");
            }

            plugins.TryAdd(parameterName, new ObjectCallback
                { GetPlugin = f => ((IServiceProvider)f["services"]).GetService(targetType) });
        }

        /// <summary>
        /// Registers a Type for PluginTests in the local thread that can be resolved when a Plugin-constructor requires a specific object-name
        /// </summary>
        /// <param name="parameterName">the name of the tester-object</param>
        /// <param name="targetType">the type of the tester-object. This will be converted to a Reflection-Only - Type</param>
        public void RegisterObjectTypeLocal(string parameterName, Type targetType)
        {
            if (currentPhase == PluginInitializationPhase.Startup)
            {
                throw new InvalidOperationException("Supported only after DependencyInjection was initialized!");
            }

            localRegistrations.Value ??= new Dictionary<string, object>();
            localRegistrations.Value[parameterName] = new ObjectCallback
                { GetPlugin = f => ((IServiceProvider)f["services"]).GetService(targetType) };
        }

        /// <summary>
        /// Registers an object for Parameter-callbacks only for the current thread
        /// </summary>
        /// <param name="parameterName">the parameter for which to register an instance</param>
        /// <param name="parameterInstance">the value to return if the factory requests the given parameter in the local thread</param>
        public void RegisterObjectLocal(string parameterName, object parameterInstance)
        {
            localRegistrations.Value ??= new Dictionary<string, object>();
            localRegistrations.Value[parameterName] = parameterInstance;
        }

        /// <summary>
        /// Clears all local Object-registrations
        /// </summary>
        public void ClearLocalRegistrations()
        {
            localRegistrations.Value?.Clear();
            localRegistrations.Value = null;
        }

        /*/// <summary>
        /// Gets a value indicating whether the provided parameter is konwn by the factory
        /// </summary>
        /// <param name="parameterName">the parameter name for which to check in this factory</param>
        /// <returns>a value indicating whether the given object is known</returns>
        public bool IsObjectRegistered(string parameterName)
        {
            return plugins.ContainsKey(parameterName) ||
                   (localRegistrations.Value?.ContainsKey(parameterName) ?? false);
        }*/

        /*/// <summary>
        /// Gets a registered obejct from this factory
        /// </summary>
        /// <param name="parameterName">the parameter that was previously registered in this factory</param>
        /// <returns>the previously registered object</returns>
        public object GetRegisteredObject(string parameterName)
        {
            object retVal = null;
            if (localRegistrations.Value?.ContainsKey(parameterName) ?? false)
            {
                retVal = localRegistrations.Value[parameterName];
            }

            if (retVal == null)
            {
                if (registeredObjects.ContainsKey(parameterName))
                {
                    retVal = registeredObjects[parameterName];
                }
            }

            return retVal;
        }*/

        /*public IEnumerator<object> GetEnumerator()
        {
            return plugins.Values.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }*/

        /// <summary>
        /// Releases all resources used by this instance
        /// </summary>
        public void Dispose()
        {
            lock (threadLock)
            {
                if (disposing)
                {
                    return;
                }

                disposing = true;
            }

            if (!this.disposed)
            {
                try
                {
                    object[] pluginArray = plugins.Values.ToArray();
                    for (int i = 0; i < pluginArray.Length; i++)
                    {
                        if (pluginArray[i] is IStoppable plugin)
                        {
                            plugin.Stop();
                        }
                    }

                    for (int i = pluginArray.Length - 1; i >= 0; i--)
                    {
                        object pi = pluginArray[i];
                        if (pi is IDisposable pd)
                        {
                            try
                            {
                                pd.Dispose();
                            }
                            catch (Exception ex)
                            {
                                LogEnvironment.LogEvent(ex.ToString(), LogSeverity.Error, "PluginSystem");
                            }
                        }
                    }

                    this.plugins.Clear();
                    this.plugins = null;
                    this.disposed = true;
                    waitForDisposedEvent.Set();
                }
                finally
                {
                    /*if (!singletonFactory)
                    {
                        SingletonEnvironment.FactoryDisposed();
                    }*/

                    OnDisposed();
                }
            }

            lock (threadLock)
            {
                disposing = false;
            }
        }

        /// <summary>
        /// Raises the UnkownConstructorParameter event
        /// </summary>
        /// <param name="e">the event arguments</param>
        protected virtual void OnUnknownConstructorParameter(UnknownConstructorParameterEventArgs e)
        {
            if (UnknownConstructorParameter != null)
            {
                UnknownConstructorParameter(this, e);
            }
        }

        /// <summary>
        /// Raises the UnkownConstructorParameter event
        /// </summary>
        /// <param name="e">the event arguments</param>
        /// <param name="sender">the original sender</param>
        protected virtual void OnUnknownConstructorParameter(object sender, UnknownConstructorParameterEventArgs e)
        {
            if (UnknownConstructorParameter != null)
            {
                UnknownConstructorParameter(sender, e);
            }
        }

        /// <summary>
        /// Raises the PluginInitialized event
        /// </summary>
        /// <param name="uniqueName">the unique name of the plugin</param>
        /// <param name="plugin">the plugin instance that was created</param>
        protected virtual void OnPluginInitialized(string uniqueName, object plugin)
        {
            if (PluginInitialized != null)
            {
                PluginInitialized(this, new PluginInitializedEventArgs(uniqueName, plugin));
            }
        }

        /// <summary>
        /// Raises the Disposed event
        /// </summary>
        protected virtual void OnDisposed()
        {
            Disposed?.Invoke(this, EventArgs.Empty);
        }


        /// <summary>
        /// Raises the ImplementGenericType event
        /// </summary>
        /// <param name="e">the requirements for constructing a type</param>
        protected virtual void OnImplementGenericType(ImplementGenericTypeEventArgs e)
        {
            ImplementGenericType?.Invoke(this, e);
        }

        /// <summary>
        /// Loads dynamic assemblies that are required by a dynamicloader for running
        /// </summary>
        private string[] LoadDynamicPlugins()
        {
            List<string> orderedNames = new List<string>();
            if (DeferredStartup)
            {
                foreach (var tmp in DynamicLoaders)
                {
                    var targetPhase = currentPhase;
                    if (targetPhase != PluginInitializationPhase.Startup)
                    {
                        targetPhase |= PluginInitializationPhase.Static;
                    }

                    orderedNames.AddRange(ProcessPlugins(tmp.GetStartupPlugins(targetPhase), null));
                }
            }

            return orderedNames.ToArray();
        }

        private IEnumerable<string> ProcessPlugins(IEnumerable<PluginInfoModel> pluginInfoList, Dictionary<string,object> customVariables)
        {
            foreach (var info in pluginInfoList)
            {
                if (!plugins.TryGetValue(info.UniqueName, out _))
                {
                    if (!pluginInitializationPromises.TryGetValue(info.UniqueName, out var loadRes))
                    {
                        TryLoadPlugin(info.UniqueName, info.ConstructorString, info.Buffer, customVariables, out _);
                        yield return info.UniqueName;
                    }
                    else if (loadRes is not null)
                    {
                        loadRes.Wait();
                    }
                }
            }
        }

        private void InitPlugin(object plugin)
        {
            if (plugin is IDeferredInit init)
            {
                if (!(configurationOnly && plugin is IConfigurableComponent))
                {
                    if (!init.Initialized)
                    {
                        init.Initialize();
                    }
                }
            }
        }

        /// <summary>
        /// Initializes the known plugins in the order they were initialized
        /// </summary>
        /// <param name="orderedNames">the names of all known plugins</param>
        private void InitializeDeferrables(string[] orderedNames)
        {
            if (DeferredStartup)
            {
                try
                {
                    foreach (string name in orderedNames)
                    {
                        if (plugins.TryGetValue(name, out var plugin))
                        {
                            InitPlugin(plugin);
                        }
                        else
                        {
                            LogEnvironment.LogDebugEvent($"Plugin {name} does not seem to be loaded properly...", LogSeverity.Warning);
                        }
                    }

                    var openPlugs = plugins.Where(n => n.Value is IDeferredInit ini && !ini.Initialized).ToArray();
                    if (openPlugs.Length != 0 && !configurationOnly)
                    {
                        LogEnvironment.LogDebugEvent("Found non-initialized Plugins. This happens when plugins load other plugins.", LogSeverity.Warning);
                        foreach (var plug in openPlugs)
                        {
                            var plugin = plug.Value;
                            InitPlugin(plugin);
                        }
                    }
                }
                finally
                {
                    CompleteCurrentPhase();
                }
            }
        }

        private void CompleteCurrentPhase()
        {
            if (currentPhase == PluginInitializationPhase.Startup)
            {
                phaseCompleted = true;
            }
            else if (currentPhase == PluginInitializationPhase.SingletonStatic)
            {
                currentPhase = PluginInitializationPhase.Singleton;
            }
            else if (currentPhase == PluginInitializationPhase.ScopeStatic)
            {
                currentPhase = PluginInitializationPhase.Scope;
            }
            else
            {
                LogEnvironment.LogDebugEvent($"CompleteCurrentPhase was called in phase {currentPhase} which is not necessary.", LogSeverity.Warning);
            }
        }

        /// <summary>
        /// Verifies a Plugin Constructor string and loads - if required the associated Plugin 
        /// </summary>
        /// <param name="uniqueName">the uniqueName of the plugin</param>
        /// <param name="pluginConstructor">the Plugin-Constructor</param>
        /// <param name="buffer">indicates whether to buffer the plugin</param>
        /// <param name="plugin">the loaded plugin</param>
        /// <param name="testOnly">indicates whether to load the plugin or to only verify the constructor</param>
        /// <returns>a value indicating whether the plugin could be successfully loaded</returns>
        private bool TryLoadPlugin(string uniqueName, string pluginConstructor, bool buffer, Dictionary<string,object> customVariables, out object plugin)
        {
            LogEnvironment.LogDebugEvent($"Loading {uniqueName} ({pluginConstructor}) with buffer={buffer}", LogSeverity.Report);
            Type pluginType;
            object[] constructor;
            plugin = null;
            ManualResetEventSlim trigger = null;
            this.ParsePluginString(uniqueName, pluginConstructor, customVariables, ref buffer, out pluginType, out constructor);
            if (!buffer || this.pluginInitializationPromises.TryAdd(uniqueName, trigger = new ManualResetEventSlim(false)))
            {
                try
                {
                    if (pluginType == null)
                    {
                        throw new InvalidOperationException(string.Format(Messages.CanNotInitializePluginError,
                                pluginConstructor));
                    }

                    //bool isSelfRegistered = false;
                    //bool isSingleton = false;
                    /*if (!testOnly)
                    {
                        isSelfRegistered = CheckSelfRegistered(pluginType);
                        //isSingleton = Attribute.IsDefined(pluginType, typeof(SingletonAttribute), true);
                    }
                    else
                    {
                        var sra = FindSelfRegisteredRoType();
                        //var sa = AssemblyResolver.FindReflectionOnlyTypeFor(typeof(SingletonAttribute));
                        var attrData = AllAttributesOf(pluginType);
                        foreach (CustomAttributeData attr in attrData)
                        {
                            if (sra.IsAssignableFrom(attr.AttributeType))
                            {
                                LogEnvironment.LogDebugEvent(null, "Found a Self-Registered Plugin...",
                                    (int)LogSeverity.Report, "PluginSystem");
                                isSelfRegistered = true;
                            }
                            /*else if (sa.IsAssignableFrom(attr.AttributeType))
                            {
                                LogEnvironment.LogDebugEvent(null, "Found a Singleton-Plugin", (int)LogSeverity.Report,
                                    "PluginSystem");
                                //isSingleton = true;
                            }*/
                        /*}
                    }*/

                    /*if (isSingleton && !singletonFactory && !testOnly)
                    {
                        plugin = SingletonEnvironment.InitializeSingletonPlugin(uniqueName, pluginConstructor, buffer,
                            OnUnknownConstructorParameter);
                        return true;

                    }*/

                    bool doBuffer = buffer;
                    /*if (isSelfRegistered)
                    {
                        object[] tmpConstructor = new object[constructor.Length + 1];
                        Array.Copy(constructor, tmpConstructor, constructor.Length);
                        if (!testOnly)
                        {
                            tmpConstructor[tmpConstructor.Length - 1] =
                                GetSelfRegistrationCallback(uniqueName, doBuffer);
                        }
                        else
                        {
                            tmpConstructor[tmpConstructor.Length - 1] = GetSelfRegistrationCallbackType();
                        }

                        constructor = tmpConstructor;
                    }*/

                    /*Type[] constructorTypes = !testOnly
                        ? Type.GetTypeArray(constructor)
                        : constructor.Cast<Type>().ToArray();*/
                    var inf = MethodHelper.GetCapableConstructor(pluginType, constructor, out var ct);
                    if (inf != null)
                    {
                        object tmp = inf.Invoke(ct);
                        /*if (tmp is SingletonPlugin sip)
                        {
                            sip.Initialize();
                            tmp = sip.Instance;
                            sip.Dispose();
                            isSelfRegistered = true;
                        }*/

                        RegisterPlugin(tmp, uniqueName, doBuffer);
                        plugin = tmp;
                        if (buffer)
                        {
                            if (plugin is ICriticalComponent crit)
                            {
                                crit.CriticalError += CriticalOccurred;
                            }

                            if (plugin is IProcessWatchDog wd)
                            {
                                wd.RegisterFor(this);
                            }

                            OnPluginInitialized(uniqueName, plugin);
                        }

                        return true;
                    }
                    else
                    {
                        throw new Exception(string.Format(Messages.NoConstructorFoundForTypeError, pluginType));
                    }
                }
                catch (Exception ex)
                {
                    LogEnvironment.LogEvent(ex.OutlineException(), LogSeverity.Error);
                    if (buffer)
                    {
                        if (!plugins.ContainsKey(uniqueName))
                        {
                            if (pluginInitializationPromises.TryRemove(uniqueName, out var wh))
                            {
                                if (wh != trigger)
                                {
                                    throw new InvalidOperationException("Something with trigger went completly wrong!");
                                }

                                wh.Set();
                                wh.Dispose();
                                wh = null;
                                trigger = null;
                            }
                            else
                            {
                                throw new InvalidOperationException("Something with promise went completly wrong!");
                            }
                        }
                        else
                        {
                            throw new InvalidOperationException("Something with register went completly wrong!");
                        }
                    }

                    throw;
                }
                finally
                {
                    trigger?.Set();
                }
            }
            else
            {
                plugin = this[uniqueName];
            }

            return true;
        }

        /*[Obsolete]
        private Type GetSelfRegistrationCallbackType()
        {
            return AssemblyResolver.FindReflectionOnlyTypeFor(typeof(SelfRegistrationCallback));
        }

        [Obsolete]
        private SelfRegistrationCallback GetSelfRegistrationCallback(string uniqueName, bool doBuffer)
        {
            return (pi) =>
            {
                RegisterPlugin(pi, uniqueName, doBuffer);
            };
        }*/

        private void RegisterPlugin(object pi, string uniqueName, bool doBuffer)
        {
            if (doBuffer)
            {
                bool ok = this.plugins.TryAdd(uniqueName, pi);
                if (ok)
                {
                    if (pi is INotifyDisposed ndpi)
                    {
                        ndpi.Disposed += this.PluginDisposal;
                    }

                    lock (orderedNames)
                    {
                        orderedNames.Add(uniqueName);
                    }
                }
                else
                {
                    if (pi is IDisposable idi)
                    {
                        idi.Dispose();
                    }

                    throw new InvalidOperationException(
                        "Failed to add Plugin to the List of available Plugins");
                }
            }

            //pi.UniqueName = uniqueName;
            LogEnvironment.LogDebugEvent(null, uniqueName, (int)LogSeverity.Report, "PluginSystem");
        }

        /*[Obsolete]
        private bool CheckSelfRegistered(Type pluginType)
        {
            return SelfRegisteredAttribute.IsDefined(pluginType, typeof(SelfRegisteredAttribute),
                true);
        }

        [Obsolete]
        private Type FindSelfRegisteredRoType()
        {
            return AssemblyResolver.FindReflectionOnlyTypeFor(typeof(SelfRegisteredAttribute));
        }*/

        private CustomAttributeData[] AllAttributesOf(Type pluginType)
        {
            var attributes = new List<CustomAttributeData>(pluginType.GetCustomAttributesData());
            while (pluginType.BaseType != null)
            {
                pluginType = pluginType.BaseType;
                attributes.AddRange(pluginType.GetCustomAttributesData());
            }

            return attributes.ToArray();
        }

        private void CriticalOccurred(object sender, CriticalErrorEventArgs args)
        {
            if (CriticalError != null &&
                args.Error.Critical)
            {
                CriticalError(this, args);
            }
        }

        /// <summary>
        /// Removes disposed plugins from the list of plugins
        /// </summary>
        /// <param name="sender">the event-sender</param>
        /// <param name="e">the event arguments</param>
        private void PluginDisposal(object sender, EventArgs e)
        {
            if (!this.disposed)
            {
                if (sender is INotifyDisposed src)
                {
                    src.Disposed -= PluginDisposal;
                }

                if (sender is ICriticalComponent crit)
                {
                    crit.CriticalError -= CriticalOccurred;
                }

                var name = GetUniqueName(sender);
                if (!string.IsNullOrEmpty(name))
                {
                    plugins.TryRemove(name, out var tmp);
                    if (tmp != sender)
                    {
                        plugins.TryAdd(name, tmp);
                    }
                }

                if (sender is IConfigurableComponent cfgComponent)
                {
                    JsonSettings.UnRegisterSettingsConsumer(cfgComponent);
                }
            }
        }

        /// <summary>
        /// Parses a constructor hint for a LogAdapter
        /// </summary>
        /// <param name="loggerString">the constructor hint</param>
        /// <param name="loggerType">the Type of the logger</param>
        /// <param name="constructor">the parsed result of the construction parameters</param>
        private void ParsePluginString(string uniqueName, string loggerString, Dictionary<string,object> customVariables, ref bool buffer, out Type loggerType, out object[] constructor)
        {
            try
            {
                Assembly a;
                PluginConstructionElement parsed =
                    PluginConstructorParser.ParsePluginString(loggerString, customVariables, stringLiteralFormatter);
                lock (registeredAssemblies)
                {
                    if (!registeredAssemblies.ContainsKey(parsed.AssemblyName))
                    {
                        string pth = Path.GetDirectoryName(parsed.AssemblyName);
                        if (pth != string.Empty)
                        {
                            if (!registeredDirectories.Contains(pth))
                            {
                                registeredDirectories.Add(pth);
                            }
                        }

                        a = AssemblyResolver.FindAssemblyByFileName(parsed.AssemblyName);
                        registeredAssemblies.Add(parsed.AssemblyName, a);
                    }
                    else
                    {
                        a = registeredAssemblies[parsed.AssemblyName];
                    }
                }

                loggerType = a.GetType(parsed.TypeName);
                if (loggerType.IsGenericTypeDefinition)
                {
                    var t = new List<GenericTypeArgument>();
                    t.AddRange(from p in loggerType.GetGenericArguments()
                        select new GenericTypeArgument { GenericTypeName = p.Name });
                    var dynLoader = DynamicLoaders.FirstOrDefault(l => l.HasParamsFor(uniqueName));
                    if (dynLoader != null)
                    {
                        dynLoader.GetGenericParams(uniqueName, t, customVariables, stringLiteralFormatter, out bool knownTypeUsed);
                        var c = (from p in t select p.TypeResult).ToArray();
                        loggerType = loggerType.MakeGenericType(c);
                        buffer &= !knownTypeUsed;
                    }
                    else
                    {
                        var arg = new ImplementGenericTypeEventArgs
                            { GenericTypes = t, PluginUniqueName = uniqueName, Formatter = stringLiteralFormatter, KnownArguments = customVariables };
                        OnImplementGenericType(arg);
                        if (arg.Handled)
                        {
                            var c = (from p in arg.GenericTypes select p.TypeResult).ToArray();
                            loggerType = loggerType.MakeGenericType(c);
                            buffer &= !arg.KnownArgumentsUsed;
                        }
                        else
                        {
                            throw new InvalidOperationException("Unable to construct generic Type");
                        }
                    }
                }

                constructor = this.ParseConstructor(parsed.Parameters, new PluginRef
                {
                    PluginType=loggerType,
                    UniqueName = uniqueName
                }, customVariables);
                LogEnvironment.LogDebugEvent(null, $"found {loggerType}...", (int)LogSeverity.Report, "PluginSystem");
            }
            catch (Exception ex)
            {
                LogEnvironment.LogEvent(ex.OutlineException(), LogSeverity.Error);
                LogEnvironment.LogEvent(loggerString, LogSeverity.Error);
                throw;
            }
        }

        /// <summary>
        /// parses the constructorparameter string for a LogAdapter
        /// </summary>
        /// <param name="constructor">the constructorparameter string</param>
        /// <param name="reflectOnly">indicates whether to only validate the constructor and therefore only to check the roTypeList for the constructor values</param>
        /// <returns>an object array containing the parsed objects</returns>
        private object[] ParseConstructor(PluginParameterElement[] constructor, PluginRef pluginType, Dictionary<string,object> customExpressionVariables)
        {
            return (from t in constructor select GetConstructorVal(t, pluginType, customExpressionVariables)).ToArray();
        }

        /// <summary>
        /// Adds a Constructor parameter to a list
        /// </summary>
        /// <param name="parameter">The Parameter for which to get the value</param>
        private object GetConstructorVal(PluginParameterElement parameter, PluginRef pluginType, Dictionary<string, object> customVariables)
        {
            object retVal = null;
            switch (parameter.TypeOfParameter)
            {
                case ParameterKind.Literal:
                    {
                        retVal = parameter.ParameterValue;
                        break;
                    }
                case ParameterKind.Plugin:
                    {
                        string value = parameter.ParameterValue.ToString();
                        retVal= GetObjectByName(value, pluginType);
                        break;
                    }

                case ParameterKind.Expression:
                {
                    var vars = new Dictionary<string, object>
                    {
                        { "Get", new Func<string, object>(name => GetObjectByName(name, pluginType)) },
                        { "PlugInType", pluginType }
                    };

                    if (customVariables != null)
                    {
                        vars["CustomArg"] = customVariables;
                    }

                    retVal = ExpressionParser.Parse(parameter.ParameterValue.ToString(), vars,
                        a => { DefaultCallbacks.PrepareDefaultCallbacks(a.Scope, a.ReplSession); });
                    break;
                }
            }

            return retVal;
        }

        private object GetObjectByName(string name, PluginRef callingType)
        {
            object retVal = null;
            if (IsPluginLoaded(name, out var ldinst))
            {
                retVal = ldinst;
            }
            else if (IsKnownPlugin(name, callingType, out var instance))
            {
                retVal = instance;
            }
            else if (name == "factory" && allowFactoryParameter)
            {
                retVal = this;
            }
            else if (name == "uniqueName")
            {
                retVal = callingType.UniqueName;
            }
            /*else if (IsObjectRegistered(name))
            {
                retVal = GetRegisteredObject(name);
            }*/
            else// if (reflectOnly || !SingletonEnvironment.FindSingletonPlugin(name, out retVal))
            {
                UnknownConstructorParameterEventArgs e = new UnknownConstructorParameterEventArgs(name, callingType);
                OnUnknownConstructorParameter(e);
                if (e.Handled)
                {
                    if (e.Value != null)
                    {
                        retVal = e.Value;
                    }
                }
                else
                {
                    throw new ArgumentException(string.Format(Messages.ConstructorValueNotFoundException, name));
                }
            }

            return retVal;
        }

        public bool IsPluginLoaded(string uniqueName, out object pluginInstance)
        {
            bool retVal = plugins.TryGetValue(uniqueName, out pluginInstance);
            if (!retVal && pluginInitializationPromises.TryGetValue(uniqueName, out var wh))
            {
                if (!wh.IsSet)
                {
                    wh.Wait(5000);
                }

                if (plugins.TryGetValue(uniqueName, out pluginInstance))
                {
                    retVal = true;
                }
            }

            if (!retVal && parent != null)
            {
                retVal = parent.IsPluginLoaded(uniqueName, out pluginInstance);
            }

            return retVal;
        }

        public bool IsKnownPlugin(string uniqueName, PluginRef callerInfo, out object pluginInstance)
        {
            var pluginIsKnown = IsPluginLoaded(uniqueName, out pluginInstance);
            if (!pluginIsKnown)
            {
                PluginInfoModel definition = null;
                var accurateLoader =
                    DynamicLoaders.FirstOrDefault(n => n.GetPluginInfo(currentPhase, uniqueName, out definition));
                if (accurateLoader != null && definition != null)
                {
                    var refPlugs = new Dictionary<string, object>();
                    if (callerInfo != null)
                    {
                        refPlugs["CallingPlugin"] = callerInfo.PluginType;
                    }

                    if (DeferredStartup && definition.Buffer)
                    {
                        LogEnvironment.LogEvent("Static Services on a scope should be initialized in the order they are actually used.", LogSeverity.Warning);
                    }


                    var preInit = accurateLoader.GetPreInitSequence(uniqueName, currentPhase);
                    ProcessPlugins(preInit, refPlugs);
                    pluginIsKnown = TryLoadPlugin(uniqueName, definition.ConstructorString, definition.Buffer, refPlugs,
                        out pluginInstance);
                    var postInit = accurateLoader.GetPostInitSequence(uniqueName, currentPhase);
                    ProcessPlugins(postInit, refPlugs);
                }
            }

            if (!pluginIsKnown && parent != null)
            {
                pluginIsKnown = parent.IsKnownPlugin(uniqueName, callerInfo, out pluginInstance);
            }

            return pluginIsKnown;
        }

        /// <summary>
        /// Is raised when this PluginFactory is disposed 
        /// </summary>
        public event EventHandler Disposed;

        /// <summary>
        /// Handles the event when the Constructor caller detects an unkown constructor parameter
        /// </summary>
        public event UnknownConstructorParameterEventHandler UnknownConstructorParameter;

        /// <summary>
        /// Provides an event informing a caller about a critical error in a loaded plugin
        /// </summary>
        public event CriticalErrorEventHandler CriticalError;

        /// <summary>
        /// Provides an event informing a caller about the initialization of a new plugin instance
        /// </summary>
        public event PluginInitializedEventHandler PluginInitialized;

        /// <summary>
        /// Is raised when a Plugin that is implemented as generic type is constructed.
        /// </summary>
        public event ImplementGenericTypeEventHandler ImplementGenericType;

        public void Start()
        {
            LoadDynamicPlugins();
        }
    }

    /// <summary>
    /// Delegate for the UnkownConstructorParameter event
    /// </summary>
    /// <param name="sender">the event-sender</param>
    /// <param name="e">the event arguments</param>
    public delegate void UnknownConstructorParameterEventHandler(object sender, UnknownConstructorParameterEventArgs e);

    /// <summary>
    /// Event arguments to the UnknownConstructorParameter event
    /// </summary>
    public class UnknownConstructorParameterEventArgs : EventArgs
    {
        /// <summary>
        /// Gets or sets the value to add to the list of constructor parameters
        /// </summary>
        public object Value { get; set; }

        /// <summary>
        /// Gets the name of the Parameter-Name Name for this Request
        /// </summary>
        public string RequestedName { get; private set; }

        /// <summary>
        /// Gets the Type of the PlugIn that is being constructed
        /// </summary>
        public PluginRef PluginType { get; }

        /// <summary>
        /// Gets or sets a value indicating whether the request could be handled by the client object
        /// </summary>
        public bool Handled { get; set; }

        /// <summary>
        /// Initializes a new instance of the UnkownConstructorParameterEventArgs class
        /// </summary>
        /// <param name="requestedName">the name of the requested value</param>
        public UnknownConstructorParameterEventArgs(string requestedName, PluginRef constructedPluginType)
            : this()
        {
            RequestedName = requestedName;
            PluginType = constructedPluginType;
        }

        /// <summary>
        /// Prevents a default instance of the UnkownConstructorParameterEventHandler class from being created
        /// </summary>
        private UnknownConstructorParameterEventArgs()
        {
        }
    }

    /// <summary>
    /// Delegate for the ImplementGenericType event
    /// </summary>
    /// <param name="sender">the event-sender</param>
    /// <param name="e">the event arguments</param>
    public delegate void ImplementGenericTypeEventHandler(object sender, ImplementGenericTypeEventArgs e);

    public class ImplementGenericTypeEventArgs : EventArgs
    {
        public string PluginUniqueName { get; set; }

        public List<GenericTypeArgument> GenericTypes { get; set; }

        internal IStringFormatProvider Formatter { get; set; }

        public bool Handled { get; set; }
        public Dictionary<string, object> KnownArguments { get; set; }
        public bool KnownArgumentsUsed { get; set; }
    }

    public class GenericTypeArgument
    {
        public string GenericTypeName { get; set; }

        public Type TypeResult { get; set; }
    }

    /// <summary>
    /// Informs a client class that a plugin has been initialized by the factory
    /// </summary>
    /// <param name="sender">the event-sender</param>
    /// <param name="e">the event arguments</param>
    public delegate void PluginInitializedEventHandler(object sender, PluginInitializedEventArgs e);

    /// <summary>
    /// Provides information for the PluginInitialized event
    /// </summary>
    public class PluginInitializedEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the PluginInitializedEventArgs class
        /// </summary>
        /// <param name="pluginName">the name of the initialized plugin</param>
        /// <param name="plugin">the plugin that has been initialized by the factory</param>
        public PluginInitializedEventArgs(string pluginName, object plugin) : this()
        {
            PluginName = pluginName;
            Plugin = plugin;
        }

        /// <summary>
        /// Prevents a default instance of the PluginInitializedEventArgs class from being created
        /// </summary>
        private PluginInitializedEventArgs()
        {
        }

        /// <summary>
        /// Gets the name of the initialized Plugin
        /// </summary>
        public string PluginName { get; private set; }

        /// <summary>
        /// Gets the plugin instance that was created
        /// </summary>
        public object Plugin { get; private set; }
    }
}