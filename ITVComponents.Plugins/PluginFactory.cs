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
using ITVComponents.AssemblyResolving;
using ITVComponents.Helpers;
using ITVComponents.Logging;
using ITVComponents.Plugins.Collections;
using ITVComponents.Plugins.Helpers;
using ITVComponents.Plugins.Initialization;
using ITVComponents.Plugins.PluginServices;
using ITVComponents.Plugins.Resources;
using ITVComponents.Plugins.Scoping;
using ITVComponents.Plugins.SelfRegistration;
using ITVComponents.Plugins.SingletonPattern;
using ITVComponents.Scripting.CScript.Core;
using ITVComponents.Scripting.CScript.Core.Methods;
using ITVComponents.Scripting.CScript.Helpers;
using ITVComponents.Settings;
using ITVComponents.Threading;

namespace ITVComponents.Plugins
{
    /// <summary>
    /// Creates Log - Adapters and passes Messages through.
    /// </summary>
    public class PluginFactory : IDelayedDisposable, ICriticalComponent, IPluginFactory
    {
        /// <summary>
        /// All Plugins that are registered in this instance
        /// </summary>
        private PluginCollector pluginInstances;

        /// <summary>
        /// Used to make sure that plugins are not being tried to load concurrently
        /// </summary>
        //private ConcurrentDictionary<string, ManualResetEventSlim> pluginInitializationPromises;

        /// <summary>
        /// All Scoped Plugins that are registered during a specific execution-scope
        /// </summary>
        private ConcurrentDictionary<PluginScope, PluginCollector> scopedPlugins;

        /// <summary>
        /// holds the current scope when a plugin-chain is initialized using a scope. Backed by either a
        /// <see cref="ThreadLocal{T}"/> or an <see cref="AsyncLocal{T}"/> depending on <see cref="scopeMode"/>;
        /// always access it through the <see cref="CurrentScope"/> property (never these fields directly).
        /// </summary>
        private readonly ThreadLocal<PluginScope> threadScope = new ThreadLocal<PluginScope>();

        private readonly AsyncLocal<PluginScope> asyncScope = new AsyncLocal<PluginScope>();

        /// <summary>
        /// Determines whether <see cref="CurrentScope"/> is thread- or async-context-bound.
        /// </summary>
        private ScopeMode scopeMode = ScopeMode.PerThread;

        /// <summary>
        /// Der Transient-Lade-Modus des aktuell laufenden Ladevorgangs. Wie <see cref="CurrentScope"/> je nach
        /// <see cref="scopeMode"/> thread- oder async-kontext-gebunden; immer ueber <see cref="TransientLoadFlag"/>
        /// zugreifen, nie direkt auf diese Felder. <c>null</c> bedeutet "kein Ladevorgang setzt das Flag" und
        /// wird als der Vorgabewert <c>true</c> gelesen.
        /// </summary>
        private readonly ThreadLocal<bool?> threadTransientLoad = new ThreadLocal<bool?>();

        private readonly AsyncLocal<bool?> asyncTransientLoad = new AsyncLocal<bool?>();

        /// <summary>
        /// A Reflection-only typelist that is used for test-only factories
        /// </summary>
        private ConcurrentDictionary<string, Type> roTypeList;

        /*/// <summary>
        /// a list of all loaded runtime serializers for this factory
        /// </summary>
        private IRuntimeSerializer runtimeSerializer;*/

        /// <summary>
        /// indicates whether this instance has been disposed
        /// </summary>
        private bool disposed = false;

        /// <summary>
        /// A List of Registered Direcories that is browsed on AssemblyLoad event. A concurrent set (the
        /// value is unused) so it can be updated lock-free alongside <see cref="registeredAssemblies"/>.
        /// </summary>
        private ConcurrentDictionary<string, byte> registeredDirectories;

        /// <summary>
        /// A list of registered assemblies used for loading plugins from dynamic assemblies. Concurrent
        /// because the new async-context scoping lets multiple threads register/resolve assemblies at the
        /// same time; the <see cref="Lazy{T}"/> value guarantees each assembly is resolved exactly once
        /// per key even under a race on <see cref="ConcurrentDictionary{TKey,TValue}.GetOrAdd(TKey,Func{TKey,TValue})"/>.
        /// </summary>
        private ConcurrentDictionary<string, Lazy<Assembly>> registeredAssemblies;

        /// <summary>
        /// indicates whether to buffer generated objects for later use
        /// </summary>
        private bool buffer = true;

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
        private bool singletonFactory = false;

        /// <summary>
        /// indicates whether this is a test-only factory
        /// </summary>
        private bool testOnlyFactory = false;

        /// <summary>
        /// Indicates whether to allow this factory to return itself when a plugin requests the parameter $factory
        /// </summary>
        private bool allowFactoryParameter = false;

        /// <summary>
        /// Indicates whether deferrable plugins should be left un-initialized
        /// </summary>
        private bool deferredStartup = false;

        /// <summary>
        /// Indicates whether the PluginFactory will put all IConfigurableComponent Plugins into a mode that will allow configuring wihtout having anything running
        /// </summary>
        private bool configurationOnly = false;

        /// <summary>
        /// Holds an instance of an object that is capable for formatting string literals before a constructor is invoked
        /// </summary>
        private StringFormatProvider stringLiteralFormatter;

        /// <summary>
        /// a list of dynamically loaded plugins
        /// </summary>
        private string[] dynamicPlugIns;

        /// <summary>
        /// An AssemblyLoad-Context that is used to load assemblies temporarly
        /// </summary>
        private AssemblyLoadContext reflectionContext;

        /// <summary>
        /// Releases a reflection-context
        /// </summary>
        private IResourceLock contextRelease;

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
        public PluginFactory() : this(true, false, false, false, false)
        {
        }

        /// <summary>
        /// Initializes a new instance of the PluginFactory class with an explicit <see cref="ScopeMode"/>.
        /// </summary>
        /// <param name="scopeMode">controls whether the active scope is thread- or async-context-bound</param>
        public PluginFactory(ScopeMode scopeMode) : this(true, false, false, false, false, scopeMode)
        {
        }

        /// <summary>
        /// Initializes a new instance of the PluginFactory class
        /// </summary>
        /// <param name="buffer">indicates whether to buffer generated objects for later use</param>
        public PluginFactory(bool buffer) : this(buffer, false, false, false, false)
        {
        }

        /// <summary>
        /// Initializes a new instance of the PluginFactory class
        /// </summary>
        /// <param name="buffer">indicates whether to buffer loaded plugins</param>
        /// <param name="reflectionFactory">indicates whether to use this factory as pluginstring verifyer only</param>
        public PluginFactory(bool buffer, bool reflectionFactory) : this(buffer, false, reflectionFactory, false, false)
        {
        }

        /// <summary>
        /// Initializes a new instance of the PluginFactory class
        /// </summary>
        /// <param name="buffer">indicates whether to buffer loaded plugins</param>
        /// <param name="reflectionFactory">indicates whether to use this factory as pluginstring verifyer only</param>
        /// <param name="deferredInitialization">indicates whether to use deferred initialization for the loaded plugins</param>
        public PluginFactory(bool buffer, bool reflectionFactory, bool deferredInitialization) : this(buffer, false,
            reflectionFactory, deferredInitialization, false)
        {
        }

        /// <summary>
        /// Initializes a new instance of the PluginFactory class
        /// </summary>
        /// <param name="buffer">indicates whether to buffer loaded plugins</param>
        /// <param name="reflectionFactory">indicates whether to use this factory as pluginstring verifyer only</param>
        /// <param name="deferredInitialization">indicates whether to use deferred initialization for the loaded plugins</param>
        /// <param name="configurationOnly">indicates whether the configurable plugins should not be initialized in order to perform configuration tasks on these components</param>
        /// <param name="scopeMode">controls whether the active scope is thread- or async-context-bound</param>
        /// <remarks>
        /// Exposes every parameter that is safe to set from outside; <c>singletonFactory</c> stays out of the
        /// public surface (it would let a caller create a second "singleton" factory and bypass the
        /// SingletonEnvironment bookkeeping). Adding <paramref name="scopeMode"/> here — instead of a separate
        /// overload — keeps the former 4-argument callers source-compatible and avoids an overload ambiguity.
        /// </remarks>
        public PluginFactory(bool buffer, bool reflectionFactory, bool deferredInitialization, bool configurationOnly, ScopeMode scopeMode = ScopeMode.PerThread) : this(buffer, false,
            reflectionFactory, deferredInitialization, configurationOnly, scopeMode)
        {
        }

        /// <summary>
        /// Initializes a new instance of the PluginFactory class
        /// </summary>
        /// <param name="buffer">indicates whether to buffer generated objects for later use</param>
        /// <param name="singletonFactory">indicates whether to use this instance as the singleton plugin instance</param>
        /// <param name="reflectionFactory">indicates whether to use this factory as test-only factory</param>
        /// <param name="deferredInitialization">indicates whether to use deferred initialization for the loaded plugins</param>
        /// <param name="configurationOnly">indicates whether the configurable plugins should not be initialized in order to perform configuration tasks on these components</param>
        internal PluginFactory(bool buffer, bool singletonFactory, bool reflectionFactory, bool deferredInitialization, bool configurationOnly, ScopeMode scopeMode = ScopeMode.PerThread)
        {
            this.scopeMode = scopeMode;
            this.singletonFactory = singletonFactory;
            this.configurationOnly = configurationOnly;
            this.buffer = buffer;
            this.testOnlyFactory = reflectionFactory;
            this.deferredStartup = deferredInitialization;
            scopedPlugins = new ConcurrentDictionary<PluginScope, PluginCollector>();
            if (!singletonFactory && !reflectionFactory)
            {
                SingletonEnvironment.FactoryInitializing();
            }

            registeredDirectories = new ConcurrentDictionary<string, byte>();
            string callingDir = Path.GetDirectoryName(Assembly.GetCallingAssembly().Location);
            if (callingDir != null)
            {
                registeredDirectories.TryAdd(callingDir, 0);
            }

            registeredAssemblies = new ConcurrentDictionary<string, Lazy<Assembly>>();
            this.pluginInstances = new PluginCollector(false);
            this.roTypeList = new ConcurrentDictionary<string, Type>();
            disposer = new Thread(Dispose);
            waitForDisposedEvent = new ManualResetEvent(false);
            if (reflectionFactory)
            {
                //reflectionContext = new AssemblyLoadContext($"ITVPI{DateTime.Now.Ticks}", true);
                contextRelease = AssemblyResolver.AcquireTemporaryLoadContext(out reflectionContext);
            }
        }

        /// <summary>
        /// Steuert, ob ein gerade geladenes Plugin in den aktiven TRANSIENTEN Ladescope aufgenommen wird
        /// (true = transient laden). Betrifft AUSSCHLIESSLICH den transienten Ladescope: ein EXPLIZITER
        /// Operations-/Fresh-Scope wird immer honoriert (siehe <see cref="HasActiveScope"/>) und kann nicht
        /// umgangen werden.
        /// </summary>
        /// <remarks>
        /// Der Wert gehoert dem gerade laufenden Ladevorgang, nicht der Factory: setzen ueber
        /// <see cref="TransientLoad"/>, damit ein genesteter Load (Abhaengigkeit) den Wert des aeusseren
        /// Ladevorgangs beim Verlassen wiederherstellt. Ein reines Zuweisen ist nur noch fuer Aufrufer da, die
        /// den Modus fuer den ganzen Kontext festlegen wollen.
        /// </remarks>
        public bool UseTransientScope
        {
            get { return TransientLoadFlag ?? true; }
            set { TransientLoadFlag = value; }
        }

        /// <summary>
        /// Setzt den Transient-Lade-Modus fuer die Dauer des zurueckgegebenen Tokens und stellt beim Freigeben
        /// den Wert wieder her, der vorher gegolten hat.
        /// </summary>
        /// <param name="transient">der Modus, der waehrend des Ladevorgangs gelten soll</param>
        /// <returns>ein Token, das den Vorwert beim Dispose wiederherstellt</returns>
        /// <remarks>
        /// Notwendig, weil die Ctor-Parameter eines Plugins ALLE aufgeloest werden, BEVOR das Plugin selbst
        /// registriert wird. Mit einer einfachen Zuweisung liest die Registrierung des aeusseren Plugins das Flag
        /// der zuletzt aufgeloesten Abhaengigkeit - ein transientes Plugin mit nicht-transienter letzter
        /// Abhaengigkeit wuerde dauerhaft, ein Singleton mit transienter letzter Abhaengigkeit fiele mit dem
        /// Ladescope. Der Handler einer Abhaengigkeit kehrt zurueck, bevor das aeussere Plugin registriert wird -
        /// das Token stellt dabei dessen eigenen Wert wieder her.
        /// </remarks>
        public IDisposable TransientLoad(bool transient)
        {
            var previous = TransientLoadFlag;
            TransientLoadFlag = transient;
            return new TransientLoadToken(this, previous);
        }

        /// <summary>
        /// The single access-point for the transient-load mode of the running load-operation. Routes to the
        /// thread- or async-local storage depending on <see cref="scopeMode"/>, exactly like
        /// <see cref="CurrentScope"/> - both must live in the SAME context, otherwise the mode tears off across
        /// <c>await</c>-boundaries while the scope survives (or vice versa).
        /// </summary>
        private bool? TransientLoadFlag
        {
            get => scopeMode == ScopeMode.PerAsyncContext ? asyncTransientLoad.Value : threadTransientLoad.Value;
            set
            {
                if (scopeMode == ScopeMode.PerAsyncContext)
                {
                    asyncTransientLoad.Value = value;
                }
                else
                {
                    threadTransientLoad.Value = value;
                }
            }
        }

        /// <summary>
        /// The single access-point for the currently-active scope. Routes to the thread- or async-local storage
        /// depending on <see cref="scopeMode"/> — this is the one place where the PerThread/PerAsyncContext switch
        /// happens; never touch <see cref="threadScope"/>/<see cref="asyncScope"/> directly.
        /// </summary>
        private PluginScope CurrentScope
        {
            get => scopeMode == ScopeMode.PerAsyncContext ? asyncScope.Value : threadScope.Value;
            set
            {
                if (scopeMode == ScopeMode.PerAsyncContext)
                {
                    asyncScope.Value = value;
                }
                else
                {
                    threadScope.Value = value;
                }
            }
        }

        /// <summary>
        /// Gets a PluginInstance with the given name
        /// </summary>
        /// <param name="pluginName">the name of the desired plugin</param>
        /// <returns>the plugin-instance with the given name</returns>
        public IPlugin this[string pluginName]
        {
            get
            {
                IPlugin retVal = plugins(null)[pluginName];
                return retVal;
            }
        }

        private StringFormatProvider ScopeFormatter
        {
            get
            {
                var retVal = stringLiteralFormatter;
                if (HasActiveScope && CurrentScope is { Formatter: not null })
                {
                    retVal = CurrentScope.Formatter;
                }

                return retVal;
            }
        }

        private bool HasActiveScope
        {
            get
            {
                // Ein EXPLIZITER (nicht-transienter) Scope ist IMMER aktiv - so kann die Factory einen
                // Operations-/Fresh-Scope nie umgehen (fruehere Gefahr: useTransientScope=false liess ein
                // Plugin an einem aktiven expliziten Scope vorbei in pluginInstances laufen und untergrub die
                // Fresh-Garantie). Ein TRANSIENTER Ladescope zaehlt nur, wenn gerade transient geladen wird.
                return (UseTransientScope && CurrentScope is { IsTransientLoadScope: true })
                       || CurrentScope is { IsTransientLoadScope: false };
            }
        }

        /// <summary>
        /// True, wenn aktuell ein expliziter (Operations-)Scope aktiv ist - also waehrend der
        /// Parameter-Aufloesung innerhalb von <c>CreateOperationScope</c>/<c>FreshInjectablePlugin</c> bzw.
        /// eines <c>NewScope</c>. Ein transienter Ladescope wird NIE als <see cref="CurrentScope"/> gefuehrt,
        /// darum bedeutet <c>CurrentScope != null</c> hier verlaesslich "in einem echten Scope".
        /// </summary>
        /// <remarks>
        /// Dient dem <c>WebPluginHelper</c>, in diesem Fall KEINEN eigenen transienten Ladescope zu oeffnen:
        /// die transient erzeugten Abhaengigkeiten gehoeren dann dem aktiven Scope und werden mit dessen
        /// Freigabe disponiert - statt in der Factory-lebenslangen Transient-Sammlung bis zum naechsten
        /// <c>ResetFactory()</c> zu ueberleben.
        /// </remarks>
        public bool IsInLoadScope => CurrentScope != null;

        /// <summary>
        /// Gets a PluginInstance with the given name
        /// </summary>
        /// <param name="pluginName">the name of the desired plugin</param>
        /// <param name="triggerAsParameterRequest">indicates whether to request the plugin as unknownparameter if it's missing in the list of loaded plugins</param>
        /// <returns>the plugin-instance with the given name</returns>
        public IPlugin this[string pluginName, bool triggerAsParameterRequest, PluginRef callingPluginRef]
        {
            get
            {
                Dictionary<string, object> dc = null;
                if (callingPluginRef != null)
                {
                    dc = new Dictionary<string, object> { { "CallingPlugin", callingPluginRef } };
                }

                if (!string.IsNullOrEmpty(pluginName))
                {
                    var uq = new UniqueNameHelper(pluginName,
                        dc, ScopeFormatter);
                    IPlugin retVal = this[uq.UniqueName];
                    if (retVal == null && triggerAsParameterRequest)
                    {
                        if (HasActiveScope)
                        {
                            retVal = RequestScopePlugin(CurrentScope, uq, callingPluginRef);
                        }

                        if (retVal == null)
                        {
                            var param = new UnknownConstructorParameterEventArgs(uq.UniqueNameRaw,
                                callingPluginRef, uq.UniqueName);
                            OnUnknownConstructorParameter(param);
                            if (param.Handled && param.Value != null)
                            {
                                retVal = (IPlugin)param.Value;
                            }
                        }

                        // Denk nicht mal dran, hier registedObjects anziehen zu wollen. Es klappt nicht, weil diese nicht zwingend
                        // IPlugin implementieren. Das ist zwar scheisse, aber hol dir das Objekt doch einfach via Konstruktor und
                        // die Sache ist gegessen. Ach ja und vergiss nicht ein Strichli zu machen.. Anzahl Versuche den "Bug",
                        // zu korrigieren. Bisher:
                        // III
                    }

                    return retVal;
                }

                return null;
            }
        }

        /// <summary>
        /// Gets a PluginInstance with the given name
        /// </summary>
        /// <param name="pluginName">the name of the desired plugin</param>
        /// <param name="triggerAsParameterRequest">indicates whether to request the plugin as unknownparameter if it's missing in the list of loaded plugins</param>
        /// <returns>the plugin-instance with the given name</returns>
        public IPlugin this[string pluginName, bool triggerAsParameterRequest] =>
            this[pluginName, triggerAsParameterRequest, null];

        /// <summary>
        /// Gets or sets a value indicating whether to allow plugins to request this factory object by having a constructor parameter called $factory
        /// </summary>
        public bool AllowFactoryParameter { get { return allowFactoryParameter; } set { allowFactoryParameter = value; } }

        /// <summary>
        /// Gets a value indicating whether the specified plugin has been initialized 
        /// </summary>
        /// <param name="uniqueName">the uniquename for which to check in the list of initialized plugins</param>
        /// <returns>a value indicating whether the requested plugin is currently reachable</returns>
        public bool Contains(string uniqueName)
        {
            return plugins(null).ContainsKey(uniqueName);
        }

        /// <summary>
        /// Creates a new plugin and uses the default buffering mode
        /// </summary>
        /// <typeparam name="T">the Type that is supposed to be created</typeparam>
        /// <param name="uniqueName">the unique name for the created plugin</param>
        /// <param name="pluginConstructor">Constructorstring for the Plugin in the Format [AssemblyPath]&lt;FullQulifiedType&gt;Parameters</param>
        /// <returns>the created IPlugin instance</returns>
        public T LoadPlugin<T>(string uniqueName, string pluginConstructor) where T : class, IPlugin
        {
            return LoadPlugin<T>(uniqueName, pluginConstructor, buffer);
        }

        public T LoadPlugin<T>(string uniqueName, string pluginConstructor, Dictionary<string,object> customVariables, bool? doBuffer = null) where T : class, IPlugin
        {
            var buffer = doBuffer ?? this.buffer;
            if (testOnlyFactory)
            {
                throw new InvalidOperationException("Unable to load a plugin in a test-only factory!");
            }

            if (disposing || disposed)
            {
                return null;
            }

            try
            {
                IPlugin retVal;
                if (TryLoadPlugin(uniqueName, pluginConstructor, buffer, customVariables, out retVal, false))
                {
                    if (retVal is IDeferredInit init)
                    {
                        if (!(configurationOnly && retVal is IConfigurableComponent))
                        {
                            if (!deferredStartup || init.ForceImmediateInitialization)
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

                    if (retVal is StringFormatProvider prov)
                    {
                        if (!HasActiveScope)
                        {
                            if (stringLiteralFormatter != null)
                            {
                                LogEnvironment.LogDebugEvent(
                                    $"There already is an instance loaded for String-formatting ({stringLiteralFormatter.UniqueName}). This instance ({retVal.UniqueName}) is being ignored.",
                                    LogSeverity.Warning);
                                return (T)retVal;
                            }

                            stringLiteralFormatter = prov;
                        }
                        else
                        {
                            CurrentScope.SetFormatter(prov);
                        }
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

        /// <summary>
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
        }

        /// <summary>
        /// Gets all loaded plugins of the specified type or interface
        /// </summary>
        /// <typeparam name="T">the desired plugin type</typeparam>
        /// <returns>an enumerable that contains all matching plugins</returns>
        public IEnumerable<T> GetPlugins<T>() where T : class, IPlugin
        {
            foreach (var t in plugins(null))
            {
                if (t.Value is T r)
                {
                    yield return r;
                }
            }
        }

        /// <summary>
        /// Gets the first Plugin that implements the specified type or interface
        /// </summary>
        /// <typeparam name="T">the desired plugin type</typeparam>
        /// <returns>the first occurrence of the specified plugin-Type or null if none was found</returns>
        public T GetPlugin<T>() where T: class, IPlugin
        {
            return GetPlugins<T>().FirstOrDefault();
        }

        /// <summary>
        /// Verifies a given constructor and returns a boolean value indicating whether the plugin-string is processable in a running environment
        /// </summary>
        /// <param name="uniqueName">the uniquename of the plugin</param>
        /// <param name="constructor">the constructor that is used to load a plugin</param>
        /// <param name="buffer">indicates whether to buffer loaded plugins</param>
        /// <returns>a value indicating whether the plugin-test was successful</returns>
        public bool VerifyConstructor(string uniqueName, string constructor, bool? buffer = null)
        {
            IPlugin dummy;
            return TryLoadPlugin(uniqueName, constructor, buffer ?? this.buffer, null, out dummy, true);
        }

        /// <summary>
        /// Registers an assembly for being used as plugin source
        /// </summary>
        /// <param name="assemblyName">the accessible name of the assembly</param>
        /// <param name="targetAssembly">the object representation of the assembly</param>
        public void RegisterAssembly(string assemblyName, Assembly targetAssembly)
        {
            // Already-known value, so the Lazy just hands it back - no resolution runs.
            if (!registeredAssemblies.TryAdd(assemblyName, new Lazy<Assembly>(() => targetAssembly)))
            {
                throw new Exception(Messages.AssemblyNameAlreadyRegisteredError);
            }
        }

        /// <summary>
        /// Resolves the CLR-Type a construction-string refers to, reflection-only - WITHOUT creating an
        /// instance. Useful for introspecting a plugin (e.g. reading class-level attributes) without
        /// loading it. Returns false (and logs) if the type cannot be resolved.
        /// </summary>
        /// <param name="constructionString">a plugin construction-string ([Assembly]&lt;FullType&gt;params)</param>
        /// <param name="pluginType">the resolved type, or null</param>
        public bool TryGetPluginType(string constructionString, out Type pluginType)
        {
            pluginType = null;
            if (string.IsNullOrWhiteSpace(constructionString))
            {
                return false;
            }

            try
            {
                PluginConstructionElement parsed =
                    PluginConstructorParser.ParsePluginString(constructionString, null, ScopeFormatter);
                Assembly a;
                if (registeredAssemblies.TryGetValue(parsed.AssemblyName, out var known))
                {
                    a = known.Value;
                }
                else
                {
                    a = AssemblyResolver.FindAssemblyByFileName(parsed.AssemblyName, reflectionContext);
                }

                pluginType = a?.GetType(parsed.TypeName);
                return pluginType != null;
            }
            catch (Exception ex)
            {
                LogEnvironment.LogEvent(
                    $"Could not resolve the plugin type for '{constructionString}': {ex.OutlineException()}",
                    LogSeverity.Warning);
                return false;
            }
        }

        /// <summary>
        /// Initializes the known plugins in the order they were initialized
        /// </summary>
        public void InitializeDeferrables()
        {
            LogEnvironment.LogDebugEvent("Legacy Implementation of InitializeDeferrables was called! Consider updating your service, as this may have unwanted side-effects.", LogSeverity.Warning);
            InitializeDeferrables(plugins(null).Names);
        }

        /// <summary>
        /// Initializes the known plugins in the order they were initialized
        /// </summary>
        /// <param name="orderedNames">the names of all known plugins</param>
        public void InitializeDeferrables(string[] orderedNames)
        {
            if (deferredStartup)
            {
                if (dynamicPlugIns != null && dynamicPlugIns.Length != 0)
                {
                    orderedNames = orderedNames.Concat(dynamicPlugIns).ToArray();
                }

                try
                {
                    var plugs = plugins(null);
                    foreach (string name in orderedNames)
                    {
                        if (plugs.TryGetValue(name, out var plugin))
                        {
                            InitPlugin(plugin);
                        }
                        else
                        {
                            LogEnvironment.LogDebugEvent($"Plugin {name} does not seem to be loaded properly...", LogSeverity.Warning);
                        }
                    }

                    var openPlugs = plugs.Where(n => n.Value is IDeferredInit ini && !ini.Initialized).ToArray();
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
                    deferredStartup = false;
                }
            }
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
            plugins(null).TryAddRegisteredObject(parameterName,
                !testOnlyFactory
                    ? parameterInstance
                    : AssemblyResolver.FindReflectionOnlyTypeFor(parameterInstance.GetType()));
        }

        /// <summary>
        /// Registers a Type for PluginTests that can be resolved when a Plugin-constructor requires a specific object-name
        /// </summary>
        /// <param name="parameterName">the name of the tester-object</param>
        /// <param name="targetType">the type of the tester-object. This will be converted to a Reflection-Only - Type</param>
        public void RegisterObjectType(string parameterName, Type targetType)
        {
            if (!testOnlyFactory)
            {
                throw new InvalidOperationException("Supported only in Test-Mode!");
            }

            plugins(null).TryAddRegisteredObject(parameterName, AssemblyResolver.FindReflectionOnlyTypeFor(targetType));
        }

        /// <summary>
        /// Registers a Type for PluginTests in the local thread that can be resolved when a Plugin-constructor requires a specific object-name
        /// </summary>
        /// <param name="parameterName">the name of the tester-object</param>
        /// <param name="targetType">the type of the tester-object. This will be converted to a Reflection-Only - Type</param>
        public void RegisterObjectTypeLocal(string parameterName, Type targetType)
        {
            if (!testOnlyFactory)
            {
                throw new InvalidOperationException("Supported only in Test-Mode!");
            }

            plugins(null).TryAddRegisteredObjectLocal(parameterName, targetType);
        }

        /// <summary>
        /// Registers an object for Parameter-callbacks only for the current thread
        /// </summary>
        /// <param name="parameterName">the parameter for which to register an instance</param>
        /// <param name="parameterInstance">the value to return if the factory requests the given parameter in the local thread</param>
        public void RegisterObjectLocal(string parameterName, object parameterInstance)
        {
            plugins(null).TryAddRegisteredObjectLocal(parameterName, !testOnlyFactory ? parameterInstance : AssemblyResolver.FindReflectionOnlyTypeFor(parameterInstance.GetType()));
        }

        /// <summary>
        /// Clears all local Object-registrations
        /// </summary>
        public void ClearLocalRegistrations()
        {
            plugins(null).ClearLocalRegistrations();
        }

        /// <summary>
        /// Gets a value indicating whether the provided parameter is konwn by the factory
        /// </summary>
        /// <param name="parameterName">the parameter name for which to check in this factory</param>
        /// <returns>a value indicating whether the given object is known</returns>
        public bool IsObjectRegistered(string parameterName)
        {
            var plugs = plugins(null);
            return plugs.IsObjectRegistered(parameterName) ||
                   plugs.IsObjectRegisteredLocal(parameterName);
        }

        /// <summary>
        /// Gets a registered obejct from this factory
        /// </summary>
        /// <param name="parameterName">the parameter that was previously registered in this factory</param>
        /// <returns>the previously registered object</returns>
        public object GetRegisteredObject(string parameterName)
        {
            object retVal = null;
            var plugs = plugins(null);
            retVal = plugs.TryGetRegisteredObjectLocal(parameterName);

            if (retVal == null)
            {
                retVal = plugs.TryGetRegisteredObject(parameterName);
            }

            return retVal;
        }

        public IEnumerator<IPlugin> GetEnumerator()
        {
            return plugins(null).Plugins.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

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
                    ClearPlugins(pluginInstances);
                    this.roTypeList.Clear();
                    this.roTypeList = null;
                    this.disposed = true;
                    waitForDisposedEvent.Set();
                }
                finally
                {
                    if (!singletonFactory)
                    {
                        SingletonEnvironment.FactoryDisposed();
                    }

                    OnDisposed();
                    this.pluginInstances = null;
                }
            }

            lock (threadLock)
            {
                disposing = false;
            }

            contextRelease?.Dispose();
            reflectionContext = null;
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
        protected virtual void OnPluginInitialized(string uniqueName, IPlugin plugin)
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

        private string[] LoadDynamicPlugins(PluginLoadType loadType, bool writeAccess)
        {
            List<string> orderedNames = new List<string>();
            foreach (var tmp in plugins(null).DynamicLoaders)
            {
                orderedNames.AddRange(tmp.LoadDynamicAssemblies(loadType, writeAccess));
            }

            return orderedNames.ToArray();
        }

        private void InitPlugin(IPlugin plugin)
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
        /// Verifies a Plugin Constructor string and loads - if required the associated Plugin 
        /// </summary>
        /// <param name="uniqueName">the uniqueName of the plugin</param>
        /// <param name="pluginConstructor">the Plugin-Constructor</param>
        /// <param name="buffer">indicates whether to buffer the plugin</param>
        /// <param name="plugin">the loaded plugin</param>
        /// <param name="testOnly">indicates whether to load the plugin or to only verify the constructor</param>
        /// <returns>a value indicating whether the plugin could be successfully loaded</returns>
        private bool TryLoadPlugin(string uniqueName, string pluginConstructor, bool buffer, Dictionary<string,object> customVariables, out IPlugin plugin, bool testOnly)
        {
            LogEnvironment.LogDebugEvent($"Loading {uniqueName} ({pluginConstructor}) with buffer={buffer}", LogSeverity.Report);
            Type pluginType;
            object[] constructor;
            plugin = null;
            ManualResetEventSlim trigger = null;
            var uq = new UniqueNameHelper(uniqueName, customVariables, ScopeFormatter);
            this.ParsePluginString(uq, pluginConstructor, customVariables, ref buffer, out pluginType, out constructor, testOnly);
            var plugs = plugins(null);
            if (testOnly || !buffer || plugs.TryInitPluginLoad(uq.UniqueName, out trigger))
            {
                try
                {
                    if (pluginType == null)
                    {
                        if (!testOnly)
                        {
                            throw new InvalidOperationException(string.Format(Messages.CanNotInitializePluginError,
                                pluginConstructor));
                        }

                        LogEnvironment.LogDebugEvent(null, string.Format(Messages.CanNotInitializePluginError,
                            pluginConstructor), (int)LogSeverity.Warning, "PluginSystem");
                        return false;
                    }

                    bool isSelfRegistered = false;
                    bool isSingleton = false;
                    if (!testOnly)
                    {
                        isSelfRegistered = CheckSelfRegistered(pluginType);
                        isSingleton = Attribute.IsDefined(pluginType, typeof(SingletonAttribute), true);
                    }
                    else
                    {
                        var sra = FindSelfRegisteredRoType();
                        var sa = AssemblyResolver.FindReflectionOnlyTypeFor(typeof(SingletonAttribute));
                        var attrData = AllAttributesOf(pluginType);
                        foreach (CustomAttributeData attr in attrData)
                        {
                            if (sra.IsAssignableFrom(attr.AttributeType))
                            {
                                LogEnvironment.LogDebugEvent(null, "Found a Self-Registered Plugin...",
                                    (int)LogSeverity.Report, "PluginSystem");
                                isSelfRegistered = true;
                            }
                            else if (sa.IsAssignableFrom(attr.AttributeType))
                            {
                                LogEnvironment.LogDebugEvent(null, "Found a Singleton-Plugin", (int)LogSeverity.Report,
                                    "PluginSystem");
                                isSingleton = true;
                            }
                        }
                    }

                    if (isSingleton && !singletonFactory && !testOnly)
                    {
                        plugin = SingletonEnvironment.InitializeSingletonPlugin(uq.UniqueName, pluginConstructor, buffer,
                            OnUnknownConstructorParameter);
                        return true;

                    }

                    bool doBuffer = buffer;
                    if (isSelfRegistered)
                    {
                        object[] tmpConstructor = new object[constructor.Length + 1];
                        Array.Copy(constructor, tmpConstructor, constructor.Length);
                        if (!testOnly)
                        {
                            tmpConstructor[tmpConstructor.Length - 1] =
                                GetSelfRegistrationCallback(uq.UniqueName, doBuffer);
                        }
                        else
                        {
                            tmpConstructor[tmpConstructor.Length - 1] = GetSelfRegistrationCallbackType();
                        }

                        constructor = tmpConstructor;
                    }

                    /*Type[] constructorTypes = !testOnly
                        ? Type.GetTypeArray(constructor)
                        : constructor.Cast<Type>().ToArray();*/
                    var inf = MethodHelper.GetCapableConstructor(pluginType, constructor, out var ct, testOnly);
                    if (inf != null && !testOnly)
                    {
                        IPlugin tmp = inf.Invoke(ct) as IPlugin;
                        if (tmp is SingletonPlugin sip)
                        {
                            sip.Initialize();
                            tmp = sip.Instance;
                            sip.Dispose();
                            isSelfRegistered = true;
                        }

                        if (!isSelfRegistered)
                        {
                            RegisterPlugin(tmp, uq.UniqueName, doBuffer);
                        }

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

                            OnPluginInitialized(uq.UniqueName, plugin);
                        }

                        return true;
                    }

                    if (inf != null)
                    {
                        plugin = null;
                        if (buffer)
                        {
                            roTypeList[uq.UniqueName] = pluginType;
                        }
                    }
                    else
                    {
                        if (!testOnly)
                        {
                            //LogEnvironment.LogEvent(string.Format(Messages.NoConstructorFoundForTypeError, pluginType), LogSeverity.Error);
                            throw new Exception(string.Format(Messages.NoConstructorFoundForTypeError, pluginType));
                        }
                        else
                        {
                            LogEnvironment.LogDebugEvent(null,
                                string.Format(Messages.NoConstructorFoundForTypeError, pluginType),
                                (int)LogSeverity.Warning, "PluginSystem");
                        }

                        return false;
                    }
                }
                catch (Exception ex)
                {
                    LogEnvironment.LogEvent(ex.OutlineException(), LogSeverity.Error);
                    if (buffer)
                    {
                        if (!plugs.ContainsKey(uq.UniqueName))
                        {
                            if (plugs.TryQuitPluginLoad(uq.UniqueName, out var wh))
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
                plugin = this[uq.UniqueName];
            }

            return true;
        }

        [Obsolete]
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
        }

        private void RegisterPlugin(IPlugin pi, string uniqueName, bool doBuffer)
        {
            if (doBuffer)
            {
                bool ok = this.plugins(null).TryAdd(uniqueName, pi);
                if (ok)
                {
                    pi.Disposed += this.PluginDisposal;
                }
                else
                {
                    pi.Dispose();
                    throw new InvalidOperationException(
                        "Failed to add Plugin to the List of available Plugins");
                }
            }

            pi.UniqueName = uniqueName;
            LogEnvironment.LogDebugEvent(null, pi.UniqueName, (int)LogSeverity.Report, "PluginSystem");
        }

        [Obsolete]
        private bool CheckSelfRegistered(Type pluginType)
        {
            return SelfRegisteredAttribute.IsDefined(pluginType, typeof(SelfRegisteredAttribute),
                true);
        }

        [Obsolete]
        private Type FindSelfRegisteredRoType()
        {
            return AssemblyResolver.FindReflectionOnlyTypeFor(typeof(SelfRegisteredAttribute));
        }

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
                if (sender is IPlugin src)
                {
                    src.Disposed -= PluginDisposal;
                    if (src is ICriticalComponent crit)
                    {
                        crit.CriticalError -= CriticalOccurred;
                    }

                    // Das Entfernen aus der Collection macht die Collection SELBST (PluginCollector
                    // abonniert beim Aufnehmen). Hier waere es falsch: plugins(null) loest den Scope zum
                    // DISPOSE-Zeitpunkt auf - ein Plugin, das in einem Scope lebt, wurde dann in der
                    // Default-Collection gesucht, nicht gefunden (tmp == null) und anschliessend ueber
                    // tmp.UniqueName mit einer NullReferenceException wieder eingefuegt.
                    if (src is IConfigurableComponent cfgComponent)
                    {
                        JsonSettings.UnRegisterSettingsConsumer(cfgComponent);
                    }
                }
            }
        }

        /// <summary>
        /// Parses a constructor hint for a LogAdapter
        /// </summary>
        /// <param name="loggerString">the constructor hint</param>
        /// <param name="loggerType">the Type of the logger</param>
        /// <param name="constructor">the parsed result of the construction parameters</param>
        /// <param name="reflectOnly">indicates whether to only validate if the provided constructor string is valid</param>
        private void ParsePluginString(UniqueNameHelper uniqueName, string loggerString, Dictionary<string,object> customVariables, ref bool buffer, out Type loggerType, out object[] constructor, bool reflectOnly)
        {
            try
            {
                PluginConstructionElement parsed =
                    PluginConstructorParser.ParsePluginString(loggerString, customVariables, ScopeFormatter);
                loggerType = ResolvePluginType(uniqueName, parsed, customVariables, reflectOnly);

                constructor = this.ParseConstructor(parsed.Parameters, new PluginRef
                {
                    PluginType=loggerType,
                    UQ = uniqueName,
                    CallingPlugin = CallerOf(customVariables)
                }, customVariables, reflectOnly);
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
        /// Resolves the CLR-type a parsed construction-element refers to - including the generic closure,
        /// if the type is a generic definition.
        /// </summary>
        /// <param name="uniqueName">the unique name of the plugin that is being described</param>
        /// <param name="parsed">the parsed construction-element</param>
        /// <param name="customVariables">the variables that are known for this load (carries CallingPlugin)</param>
        /// <param name="reflectOnly">indicates whether the type is resolved for verification only</param>
        /// <returns>the resolved (closed) type</returns>
        private Type ResolvePluginType(UniqueNameHelper uniqueName, PluginConstructionElement parsed,
            Dictionary<string, object> customVariables, bool reflectOnly)
        {
            var retVal = ResolveRawPluginType(parsed, reflectOnly);
            if (retVal.IsGenericTypeDefinition)
            {
                retVal = CloseGenericType(uniqueName, retVal, customVariables);
            }

            return retVal;
        }

        /// <summary>
        /// Resolves the assembly and the CLR-type of a parsed construction-element. A generic plugin comes
        /// back as its OPEN definition - closing it is a separate step, because that evaluates configured
        /// expressions and can fail on its own.
        /// </summary>
        private Type ResolveRawPluginType(PluginConstructionElement parsed, bool reflectOnly)
        {
            Assembly a;
            // Lock-free: the ConcurrentDictionary handles the races the former lock guarded, and the
            // Lazy value makes sure a given assembly is resolved (loaded) exactly once even if two
            // threads request the same key simultaneously.
            if (registeredAssemblies.TryGetValue(parsed.AssemblyName, out var known))
            {
                a = known.Value;
            }
            else
            {
                string pth = Path.GetDirectoryName(parsed.AssemblyName);
                if (!string.IsNullOrEmpty(pth))
                {
                    registeredDirectories.TryAdd(pth, 0);
                }

                if (reflectOnly)
                {
                    // Reflect-only: resolve transiently, do NOT cache (mirrors the former behaviour).
                    a = AssemblyResolver.FindAssemblyByFileName(parsed.AssemblyName, reflectionContext);
                }
                else
                {
                    a = registeredAssemblies.GetOrAdd(parsed.AssemblyName,
                        name => new Lazy<Assembly>(
                            () => AssemblyResolver.FindAssemblyByFileName(name, reflectionContext))).Value;
                }
            }

            return a.GetType(parsed.TypeName);
        }

        /// <summary>
        /// Closes a generic plugin-type by resolving its configured type-arguments.
        /// </summary>
        private Type CloseGenericType(UniqueNameHelper uniqueName, Type openType,
            Dictionary<string, object> customVariables)
        {
            var t = new List<GenericTypeArgument>();
            t.AddRange(from p in openType.GetGenericArguments()
                select new GenericTypeArgument { GenericTypeName = p.Name });
            var dynLoader = plugins(null).DynamicLoaders.FirstOrDefault(l => l.HasParamsFor(uniqueName.UniqueNameRaw));
            if (dynLoader != null)
            {
                dynLoader.GetGenericParams(uniqueName.UniqueNameRaw, t, customVariables, ScopeFormatter/*, out bool knownTypeUsed*/);
                var c = (from p in t select p.TypeResult).ToArray();
                return openType.MakeGenericType(c);
            }

            var arg = new ImplementGenericTypeEventArgs
                { GenericTypes = t, PluginUniqueName = uniqueName.UniqueNameRaw, Formatter = ScopeFormatter, KnownArguments = customVariables };
            OnImplementGenericType(arg);
            if (arg.Handled)
            {
                var c = (from p in arg.GenericTypes select p.TypeResult).ToArray();
                return openType.MakeGenericType(c);
            }

            throw new InvalidOperationException("Unable to construct generic Type");
        }

        /// <summary>
        /// Reads the caller out of a set of load-variables. Das ist die Stelle, an der die Aufrufkette
        /// zusammengesetzt wird: der Aufrufer ist selbst schon verkettet, also haengt mit ihm der ganze
        /// bisherige Weg am neuen <see cref="PluginRef"/>.
        /// </summary>
        private static PluginRef CallerOf(Dictionary<string, object> customVariables)
        {
            if (customVariables != null && customVariables.TryGetValue("CallingPlugin", out var caller))
            {
                return caller as PluginRef;
            }

            return null;
        }

        /// <summary>
        /// Beschreibt ein Plugin, OHNE es zu bauen: loest Assembly und Typ (inklusive generischer
        /// Schliessung) auf und liefert einen bereits verketteten <see cref="PluginRef"/>.
        /// </summary>
        /// <param name="uniqueName">der eindeutige Name des Plugins</param>
        /// <param name="pluginConstructor">der Konstruktor-String des Plugins</param>
        /// <param name="callingPluginRef">der Anforderer dieses Plugins - wird zum Vorgaenger in der Kette</param>
        /// <returns>der beschreibende PluginRef, oder <c>null</c> wenn der Typ nicht aufloesbar ist</returns>
        /// <remarks>
        /// Gedacht fuer Aufrufer, die einen PluginRef brauchen, BEVOR das Plugin existiert - etwa fuer eine
        /// Init-Sequenz, die dem Plugin gehoert und darum seinen eigenen Ref als <c>CallingPlugin</c> sehen
        /// muss, nicht den seines Anforderers. Achtung: die generische Aufloesung laeuft dabei ein zweites
        /// Mal (das <c>ImplementGenericType</c>-Event feuert erneut) - nur aufrufen, wenn der Ref
        /// tatsaechlich gebraucht wird.
        /// </remarks>
        public PluginRef DescribePlugin(string uniqueName, string pluginConstructor, PluginRef callingPluginRef)
        {
            if (string.IsNullOrEmpty(uniqueName) || string.IsNullOrEmpty(pluginConstructor))
            {
                return null;
            }

            Dictionary<string, object> dc = null;
            if (callingPluginRef != null)
            {
                dc = new Dictionary<string, object> { { "CallingPlugin", callingPluginRef } };
            }

            var uq = new UniqueNameHelper(uniqueName, dc, ScopeFormatter);
            try
            {
                var parsed = PluginConstructorParser.ParsePluginString(pluginConstructor, dc, ScopeFormatter);
                var pluginType = ResolveRawPluginType(parsed, false);
                if (pluginType.IsGenericTypeDefinition)
                {
                    try
                    {
                        pluginType = CloseGenericType(uq, pluginType, dc);
                    }
                    catch (Exception ex)
                    {
                        // Henne und Ei: die Typ-Argumente eines generischen Plugins stehen als Ausdruecke in
                        // der Konfiguration und zeigen ueblicherweise selbst auf CallingPlugin - also auf die
                        // Kette, die hier gerade erst aufgebaut wird. Scheitert das, bleibt der GESCHLOSSENE
                        // Typ unbekannt; Name und Kette sind aber vollstaendig. Ein Ref mit offener Definition
                        // ist dann deutlich besser als gar keiner: sonst faellt der Aufrufer auf den Ref des
                        // ANFORDERERS zurueck und die Kette ist wieder um eine Stufe verschoben - genau der
                        // Fehler, den DescribePlugin beheben soll.
                        LogEnvironment.LogEvent(
                            $"Could not close the generic type of '{uniqueName}' while describing it, so the "
                            + $"calling-chain carries this plugin without its closed type ({pluginType}). "
                            + $"Cause: {ex.OutlineException()}",
                            LogSeverity.Warning);
                    }
                }

                return new PluginRef
                {
                    PluginType = pluginType,
                    UQ = uq,
                    CallingPlugin = callingPluginRef
                };
            }
            catch (Exception ex)
            {
                // Bewusst kein Wurf: der Aufrufer faellt auf den Ref des Anforderers zurueck. Aber die
                // Ursache muss im Log stehen, sonst ist eine falsche CallingPlugin-Aufloesung unauffindbar.
                LogEnvironment.LogEvent(
                    $"Could not describe the plugin '{uniqueName}' ({pluginConstructor}): {ex.OutlineException()}",
                    LogSeverity.Warning);
                return null;
            }
        }

        /// <summary>
        /// parses the constructorparameter string for a LogAdapter
        /// </summary>
        /// <param name="constructor">the constructorparameter string</param>
        /// <param name="reflectOnly">indicates whether to only validate the constructor and therefore only to check the roTypeList for the constructor values</param>
        /// <returns>an object array containing the parsed objects</returns>
        private object[] ParseConstructor(PluginParameterElement[] constructor, PluginRef pluginType, Dictionary<string,object> customExpressionVariables, bool reflectOnly)
        {
            return (from t in constructor select GetConstructorVal(t, pluginType, customExpressionVariables, reflectOnly)).ToArray();
        }

        /// <summary>
        /// Adds a Constructor parameter to a list
        /// </summary>
        /// <param name="parameter">The Parameter for which to get the value</param>
        /// <param name="reflectOnly">indicates whether to only verify constructors and therefore check the roTypeList instead of the PluginList</param>
        private object GetConstructorVal(PluginParameterElement parameter, PluginRef pluginType, Dictionary<string, object> customVariables, bool reflectOnly)
        {
            object retVal = null;
            // Die eingehenden Variablen bleiben erhalten - alles, was der Host sonst mitgibt, war eine Stufe
            // tiefer bisher verschwunden. Ersetzt wird nur CallingPlugin: fuer die naechste Stufe ist DIESES
            // Plugin der Aufrufer.
            Dictionary<string, object> dc = customVariables != null
                ? new Dictionary<string, object>(customVariables)
                : null;
            if (pluginType != null)
            {
                dc ??= new Dictionary<string, object>();
                dc["CallingPlugin"] = pluginType;
            }
            switch (parameter.TypeOfParameter)
            {
                case ParameterKind.Literal:
                    {
                        retVal = parameter.ParameterValue;
                        if (reflectOnly)
                        {
                            retVal = retVal?.GetType();
                        }
                        break;
                    }
                case ParameterKind.Plugin:
                    {
                        string value = parameter.ParameterValue.ToString();
                        var tmpUQ = new UniqueNameHelper(value,
                            dc, ScopeFormatter);
                        retVal= GetObjectByName(tmpUQ, pluginType, reflectOnly);
                        break;
                    }

                case ParameterKind.Expression:
                {
                    var vars = new Dictionary<string, object>
                    {
                        { "Get", new Func<string, object>(name => GetObjectByName(new UniqueNameHelper(name,
                            dc, ScopeFormatter), pluginType, reflectOnly)) },
                        { "PlugInType", pluginType }
                    };

                    if (customVariables != null)
                    {
                        vars["CustomArg"] = customVariables;
                    }

                    retVal = ExpressionParser.Parse(parameter.ParameterValue.ToString(), vars,
                        a => { DefaultCallbacks.PrepareDefaultCallbacks(a.Scope, a.ReplSession); });
                    if (reflectOnly)
                    {
                        if (retVal != null)
                        {
                            retVal = AssemblyResolver.FindReflectionOnlyTypeFor(retVal.GetType());
                        }
                    }

                    break;
                }
            }

            return retVal;
        }

        private object GetObjectByName(UniqueNameHelper name, PluginRef callingType, bool reflectOnly)
        {
            object retVal = null;
            var plugs = plugins(null);
            if (plugs.ContainsKey(name.UniqueName) || (reflectOnly
                                                   && roTypeList.ContainsKey(name.UniqueName)))
            {
                if (!reflectOnly)
                {
                    retVal = plugs[name.UniqueName];
                }
                else
                {
                    retVal = roTypeList[name.UniqueName];
                }
            }
            else if (name.UniqueName == "factory" && allowFactoryParameter)
            {
                retVal = this;
                if (reflectOnly)
                {
                    retVal = AssemblyResolver.FindReflectionOnlyTypeFor(retVal.GetType());
                }
            }
            else if (name.UniqueName == "ifactory" && allowFactoryParameter && HasActiveScope)
            {
                retVal = CurrentScope;
                if (reflectOnly)
                {
                    retVal = AssemblyResolver.FindReflectionOnlyTypeFor(typeof(IPluginFactory));
                }
            }
            else if (IsObjectRegistered(name.UniqueName))
            {
                retVal = GetRegisteredObject(name.UniqueName);
            }
            else if (reflectOnly || !SingletonEnvironment.FindSingletonPlugin(name.UniqueName, out retVal))
            {
                if (HasActiveScope)
                {
                    retVal = RequestScopePlugin(CurrentScope, name, callingType);
                }

                if (retVal == null)
                {
                    UnknownConstructorParameterEventArgs e =
                        new UnknownConstructorParameterEventArgs(name.UniqueNameRaw, callingType,
                            name.UniqueName);
                    OnUnknownConstructorParameter(e);
                    if (e.Handled)
                    {
                        if (e.Value != null)
                        {
                            retVal = e.Value;
                            if (reflectOnly)
                            {
                                retVal = AssemblyResolver.FindReflectionOnlyTypeFor(retVal.GetType());
                            }
                        }
                    }
                    else
                    {
                        throw new ArgumentException(string.Format(Messages.ConstructorValueNotFoundException,
                            $"{name.UniqueNameRaw} (->{name.UniqueName})"));
                    }
                }
            }

            return retVal;
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

        public void LoadDynamics(bool writeAccess = true)
        {
            dynamicPlugIns = LoadDynamicPlugins(PluginLoadType.Singleton, writeAccess);
        }

        public IPlugin[] ScopeClose()
        {
            if (HasActiveScope)
            {
                return CurrentScope.ScopeClose();
            }

            return Array.Empty<IPlugin>();
        }

        internal IPlugin[] CloseScope(PluginScope scope)
        {
            IPlugin[] retVal = Array.Empty<IPlugin>();
            if (scopedPlugins.TryRemove(scope, out var pluginDic))
            {
                retVal = ClearPlugins(pluginDic);
            }

            return retVal;
        }

        internal T WithScope<T>(PluginScope scope, Func<PluginScope, T> action)
        {
            if (HasActiveScope && CurrentScope != scope)
            {
                throw new InvalidOperationException("There already is a plugin-load in progress in this thread!");
            }

            bool currentScopeSet = false;
            if (!HasActiveScope)
            {
                CurrentScope = scope;
                currentScopeSet = true;
            }

            try
            {
                return action(scope);
            }
            finally
            {
                if (currentScopeSet)
                {
                    CurrentScope = null;
                }
            }
        }
        internal IPlugin RequestScopePlugin(PluginScope pluginScope, UniqueNameHelper pluginName, PluginRef callingPluginRef)
        {

            return WithScope<IPlugin>(pluginScope, s =>
            {

                var plugs = plugins(pluginScope);
                var loaders = plugs.DynamicLoaders;
                IDynamicLoader loader;
                if (loaders.Length > 1)
                {
                    // FirstOrDefault, not First: with several loaders present (now that scopes also see the
                    // main factory's loaders) it is normal that none of them knows this particular name -
                    // that must yield null, not throw.
                    loader = loaders.FirstOrDefault(n => n.HasScopedPlugin(pluginName.UniqueNameRaw));
                }
                else if (loaders.Length == 1)
                {
                    loader = loaders[0];
                }
                else
                {
                    loader = null;
                }

                if (loader != null)
                {
                    var definition = loader.GetScopedPlugin(pluginName.UniqueNameRaw);
                    if (definition != null)
                    {
                        var dc = new Dictionary<string, object>();
                        if (callingPluginRef != null)
                        {
                            dc.Add("CallingPlugin", callingPluginRef);
                        }

                        return LoadPlugin<IPlugin>(definition.Name, definition.ConstructionString, dc);
                    }
                }

                return null;
            });
        }

        private IPlugin[] ClearPlugins(PluginCollector pluginDic)
        {
            return pluginDic.Clear();
        }

        private PluginCollector plugins(PluginScope scope)
        {
            if (HasActiveScope)
            {
                scope ??= CurrentScope;
            }

            if (scope != null)
            {
                return scopedPlugins[scope];
            }

            return pluginInstances;
        }

        public IPluginFactory NewScope(Dictionary<string, object> knownScopeObjects, IServiceProvider services, bool transientLoadingScope, ISet<string> disposeWithScope = null)
        {
            var scopePlugins = new PluginCollector(pluginInstances, transientLoadingScope);
            var retVal = new PluginScope(this, scopePlugins);
            scopedPlugins.TryAdd(retVal, scopePlugins);
            if (!transientLoadingScope)
            {
                if (knownScopeObjects != null)
                {
                    foreach (var kso in knownScopeObjects)
                    {
                        scopePlugins.TryAddRegisteredObject(kso.Key, kso.Value, disposeWithScope?.Contains(kso.Key) ?? false);
                    }
                }

                if (services != null)
                {
                    scopePlugins.TryAddRegisteredObject("services", services);
                }

                retVal = WithScope(retVal, s =>
                {
                    var scopeInitializers =
                        (from t in scopePlugins.DynamicLoaders
                            select t.LoadDynamicAssemblies(PluginLoadType.ScopeStartup))
                        .SelectMany(n => n).ToArray();
                    LogEnvironment.LogDebugEvent($"{scopeInitializers.Length} plugins loaded for Scope-Startup.",
                        LogSeverity.Report);
                    return s;
                });
            }

            return retVal;
        }

        /// <summary>
        /// Restores the transient-load mode that was in effect before the <see cref="TransientLoad"/> call that
        /// created this token.
        /// </summary>
        private sealed class TransientLoadToken : IDisposable
        {
            private readonly PluginFactory owner;

            private readonly bool? previous;

            private bool disposed;

            public TransientLoadToken(PluginFactory owner, bool? previous)
            {
                this.owner = owner;
                this.previous = previous;
            }

            public void Dispose()
            {
                if (disposed)
                {
                    return;
                }

                disposed = true;
                owner.TransientLoadFlag = previous;
            }
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
        /// <remarks>
        /// Der Name, wie er in der Konfiguration STEHT - also gegebenenfalls noch als Ausdruck
        /// (<c>$Name[CallingPlugin.UniqueName]</c>). Er ist der Schluessel, unter dem eine
        /// Plugin-Definition abgelegt ist; was der Ausdruck fuer DIESEN Ladevorgang bedeutet, steht in
        /// <see cref="ResolvedName"/>.
        /// </remarks>
        public string RequestedName { get; private set; }

        /// <summary>
        /// Der aufgeloeste Name dieses Ladevorgangs - der Name, unter dem das Plugin danach in der
        /// Factory steht. Bei einem Namen ohne Ausdruck derselbe wie <see cref="RequestedName"/>.
        /// </summary>
        /// <remarks>
        /// Da, damit ein Behandler zwischen "der Vorgabe fuer alle" und "der Abweichung fuer genau
        /// diesen" unterscheiden kann: eine Plugin-Definition <c>$SqlOptionsLoader4[CallingPlugin.UniqueName]</c>
        /// wird fuer jeden Anforderer einmal geladen, und der Behandler sieht ohne diesen Wert nur den
        /// gemeinsamen Ausdruck - also fuer alle dasselbe.
        /// </remarks>
        public string ResolvedName { get; private set; }

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
        /// <param name="constructedPluginType">der Anforderer dieses Plugins</param>
        /// <param name="resolvedName">
        /// der aufgeloeste Name; null bedeutet "derselbe wie <paramref name="requestedName"/>"
        /// </param>
        public UnknownConstructorParameterEventArgs(string requestedName, PluginRef constructedPluginType,
            string resolvedName = null)
            : this()
        {
            RequestedName = requestedName;
            ResolvedName = resolvedName ?? requestedName;
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

        internal StringFormatProvider Formatter { get; set; }

        public bool Handled { get; set; }
        public Dictionary<string, object> KnownArguments { get; set; }
        //public bool KnownArgumentsUsed { get; set; }
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
        public PluginInitializedEventArgs(string pluginName, IPlugin plugin) : this()
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
        public IPlugin Plugin { get; private set; }
    }
}