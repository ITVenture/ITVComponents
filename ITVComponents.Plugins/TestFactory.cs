using Antlr4.Runtime;
using ITVComponents.AssemblyResolving;
using ITVComponents.Plugins.PluginServices;
using ITVComponents.Threading;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Scripting.CScript.Core;
using ITVComponents.Scripting.CScript.Helpers;
using ITVComponents.Plugins.Resources;
using ITVComponents.Helpers;
using ITVComponents.Logging;
using ITVComponents.Plugins.Initialization;
using ITVComponents.Scripting.CScript.Core.Methods;
using ITVComponents.Settings;
using System.IO;
using System.Threading;

namespace ITVComponents.Plugins
{
    public class TestFactory:IPluginFactory
    {
        /// <summary>
        /// All Plugins that are registered in this instance
        /// </summary>
        private ConcurrentDictionary<string, Type> plugins;

        /// <summary>
        /// All objects that can be accessed directly as constructor parameters when an object requests it
        /// </summary>
        private ConcurrentDictionary<string, Type> registeredObjects;


        private AsyncLocal<Dictionary<string, Type>> localRegistrations;

        /*/// <summary>
        /// a list of all loaded runtime serializers for this factory
        /// </summary>
        private IRuntimeSerializer runtimeSerializer;*/

        /// <summary>
        /// indicates whether this instance has been disposed
        /// </summary>
        private bool disposed = false;

        /// <summary>
        /// object to synchronize the usage of resources
        /// </summary>
        private object threadLock = new object();

        /*/// <summary>
        /// Holds the latest runtime status of all plugins providing it
        /// </summary>
        private RuntimeInformation runtimeStatus = new RuntimeInformation();*/

        /// <summary>
        /// Indicates whether to allow this factory to return itself when a plugin requests the parameter $factory
        /// </summary>
        private bool allowFactoryParameter = false;

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
        static TestFactory()
        {
            AssemblyResolver.Enabled = true;
        }

        /// <summary>
        /// Initializes a new instance of the PluginFactory class
        /// </summary>
        public TestFactory()
        {
            this.plugins = new ConcurrentDictionary<string, Type>();
            registeredObjects = new ConcurrentDictionary<string, Type>();
            localRegistrations = new AsyncLocal<Dictionary<string, Type>>();//() => new Dictionary<string, object>());
            contextRelease = AssemblyResolver.AcquireTemporaryLoadContext(out reflectionContext);
        }

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
            Array.Empty<IDynamicLoader>();

        /// <summary>
        /// Gets a value indicating whether the specified plugin has been initialized 
        /// </summary>
        /// <param name="uniqueName">the uniquename for which to check in the list of initialized plugins</param>
        /// <returns>a value indicating whether the requested plugin is currently reachable</returns>
        public bool Contains(string uniqueName)
        {
            return plugins.ContainsKey(uniqueName);
        }

        public T LoadPlugin<T>(string uniqueName, string pluginConstructor, Dictionary<string, object> customVariables) where T : class
        {
            if (disposed)
            {
                return null;
            }

            try
            {
                Type retVal;
                VerifyConstructor(uniqueName, pluginConstructor);
            }
            catch (Exception ex)
            {
            }

            return default(T);
        }

        public bool IsKnownPlugin(string uniqueName, PluginRef callerInfo, out object pluginInstance)
        {
            pluginInstance = null;
            return false;
        }

        public bool IsPluginLoaded(string uniqueName, out object pluginInstance)
        {
            pluginInstance = null;
            return false;
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
            return TryLoadPlugin(uniqueName, constructor);
        }

        /// <summary>
        /// Registers an assembly for being used as plugin source
        /// </summary>
        /// <param name="assemblyName">the accessible name of the assembly</param>
        /// <param name="targetAssembly">the object representation of the assembly</param>
        public void RegisterAssembly(string assemblyName, Assembly targetAssembly)
        {
        }

        /// <summary>
        /// Initializes the known plugins in the order they were initialized
        /// </summary>
        public void InitializeDeferrables()
        {
        }

        /// <summary>
        /// Initializes the known plugins in the order they were initialized
        /// </summary>
        /// <param name="orderedNames">the names of all known plugins</param>
        public void InitializeDeferrables(string[] orderedNames)
        {
        }

        /// <summary>
        /// Tries to dispose the object and waits for the provided timeout
        /// </summary>
        /// <param name="timeout">the timeout to wait for the object to dispose</param>
        /// <returns>a value indicating whether the object could be disposed</returns>
        public bool Dispose(int timeout)
        {
            return true;
        }

        /// <summary>
        /// Registers an object that can be resolved when a creating object requests a constructor parameter
        /// </summary>
        /// <param name="parameterName">the name of the used constructor parameter</param>
        /// <param name="parameterInstance">the value that can be accessed used the provided parametername</param>
        public void RegisterObject(string parameterName, object parameterInstance)
        {
            registeredObjects.TryAdd(parameterName,
                 AssemblyResolver.FindReflectionOnlyTypeFor(parameterInstance.GetType()));
        }

        /// <summary>
        /// Registers a Type for PluginTests that can be resolved when a Plugin-constructor requires a specific object-name
        /// </summary>
        /// <param name="parameterName">the name of the tester-object</param>
        /// <param name="targetType">the type of the tester-object. This will be converted to a Reflection-Only - Type</param>
        public void RegisterObjectType(string parameterName, Type targetType)
        {
            registeredObjects.TryAdd(parameterName, AssemblyResolver.FindReflectionOnlyTypeFor(targetType));
        }

        /// <summary>
        /// Registers a Type for PluginTests in the local thread that can be resolved when a Plugin-constructor requires a specific object-name
        /// </summary>
        /// <param name="parameterName">the name of the tester-object</param>
        /// <param name="targetType">the type of the tester-object. This will be converted to a Reflection-Only - Type</param>
        public void RegisterObjectTypeLocal(string parameterName, Type targetType)
        {
            localRegistrations.Value ??= new Dictionary<string, Type>();
            localRegistrations.Value[parameterName] = targetType;
        }

        /// <summary>
        /// Registers an object for Parameter-callbacks only for the current thread
        /// </summary>
        /// <param name="parameterName">the parameter for which to register an instance</param>
        /// <param name="parameterInstance">the value to return if the factory requests the given parameter in the local thread</param>
        public void RegisterObjectLocal(string parameterName, object parameterInstance)
        {
            localRegistrations.Value ??= new Dictionary<string, Type>();
            localRegistrations.Value[parameterName] = AssemblyResolver.FindReflectionOnlyTypeFor(parameterInstance.GetType());
        }

        /// <summary>
        /// Clears all local Object-registrations
        /// </summary>
        public void ClearLocalRegistrations()
        {
            localRegistrations.Value?.Clear();
            localRegistrations.Value = null;
        }

        public string GetUniqueName(object plugin)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Gets a value indicating whether the provided parameter is konwn by the factory
        /// </summary>
        /// <param name="parameterName">the parameter name for which to check in this factory</param>
        /// <returns>a value indicating whether the given object is known</returns>
        public bool IsObjectRegistered(string parameterName)
        {
            return registeredObjects.ContainsKey(parameterName) ||
                   (localRegistrations.Value?.ContainsKey(parameterName) ?? false);
        }

        /// <summary>
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
        }

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
            OnDisposed();
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
        protected virtual void OnPluginInitialized(string uniqueName, Type plugin)
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
        /// Verifies a Plugin Constructor string and loads - if required the associated Plugin 
        /// </summary>
        /// <param name="uniqueName">the uniqueName of the plugin</param>
        /// <param name="pluginConstructor">the Plugin-Constructor</param>
        /// <param name="buffer">indicates whether to buffer the plugin</param>
        /// <param name="plugin">the loaded plugin</param>
        /// <param name="testOnly">indicates whether to load the plugin or to only verify the constructor</param>
        /// <returns>a value indicating whether the plugin could be successfully loaded</returns>
        private bool TryLoadPlugin(string uniqueName, string pluginConstructor)
        {
            Type pluginType;
            object[] constructor;
            this.ParsePluginString(uniqueName, pluginConstructor, out pluginType, out constructor);
            if (pluginType != null)
            {
                var inf = MethodHelper.GetCapableConstructor(pluginType, constructor, out var ct);
                if (inf != null)
                {
                    return plugins.TryAdd(uniqueName, pluginType);
                }

                LogEnvironment.LogDebugEvent(null,
                    string.Format(Messages.NoConstructorFoundForTypeError, pluginType),
                    (int)LogSeverity.Warning, "PluginSystem");
            }

            LogEnvironment.LogDebugEvent(null,
                string.Format(Messages.NoConstructorFoundForTypeError, uniqueName),
                (int)LogSeverity.Warning, "PluginSystem");
            return false;
        }

        /// <summary>
        /// Parses a constructor hint for a LogAdapter
        /// </summary>
        /// <param name="loggerString">the constructor hint</param>
        /// <param name="loggerType">the Type of the logger</param>
        /// <param name="constructor">the parsed result of the construction parameters</param>
        /// <param name="reflectOnly">indicates whether to only validate if the provided constructor string is valid</param>
        private void ParsePluginString(string uniqueName, string loggerString, out Type loggerType, out object[] constructor)
        {
            try
            {
                Assembly a;
                PluginConstructionElement parsed =
                    PluginConstructorParser.ParsePluginString(loggerString, null, null);
                a = AssemblyResolver.FindAssemblyByFileName(parsed.AssemblyName, reflectionContext);
                loggerType = a.GetType(parsed.TypeName);
                if (loggerType.IsGenericTypeDefinition)
                {
                    var t = new List<GenericTypeArgument>();
                    t.AddRange(from p in loggerType.GetGenericArguments()
                               select new GenericTypeArgument { GenericTypeName = p.Name });
                }

                constructor = this.ParseConstructor(parsed.Parameters, new PluginRef
                {
                    PluginType = loggerType,
                    UniqueName = uniqueName
                }, null);
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
        private object[] ParseConstructor(PluginParameterElement[] constructor, PluginRef pluginType, Dictionary<string, object> customExpressionVariables)
        {
            return (from t in constructor select GetConstructorVal(t, pluginType, customExpressionVariables)).ToArray();
        }

        /// <summary>
        /// Adds a Constructor parameter to a list
        /// </summary>
        /// <param name="parameter">The Parameter for which to get the value</param>
        /// <param name="reflectOnly">indicates whether to only verify constructors and therefore check the roTypeList instead of the PluginList</param>
        private object GetConstructorVal(PluginParameterElement parameter, PluginRef pluginType, Dictionary<string, object> customVariables)
        {
            object retVal = null;
            switch (parameter.TypeOfParameter)
            {
                case ParameterKind.Literal:
                    {
                        retVal = parameter.ParameterValue;
                        retVal = retVal?.GetType();
                        break;
                    }
                case ParameterKind.Plugin:
                    {
                        string value = parameter.ParameterValue.ToString();
                        retVal = GetObjectByName(value, pluginType);
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
                    if (retVal != null)
                    {
                        retVal = AssemblyResolver.FindReflectionOnlyTypeFor(retVal.GetType());
                    }

                    break;
                }
            }

            return retVal;
        }

        private object GetObjectByName(string name, PluginRef callingType)
        {
            object retVal = null;
            if (this.plugins.ContainsKey(name))
            {
                retVal = this.plugins[name];
            }
            else if (name == "factory" && allowFactoryParameter)
            {
                retVal = typeof(IPluginFactory);
            }
            else if (IsObjectRegistered(name))
            {
                retVal = GetRegisteredObject(name);
            }
            else
            {
                UnknownConstructorParameterEventArgs e = new UnknownConstructorParameterEventArgs(name, callingType);
                OnUnknownConstructorParameter(e);
                if (e.Handled)
                {
                    if (e.Value != null)
                    {
                        retVal = AssemblyResolver.FindReflectionOnlyTypeFor(e.Value?.GetType());
                    }
                }
                else
                {
                    throw new ArgumentException(string.Format(Messages.ConstructorValueNotFoundException, name));
                }
            }

            return retVal;
        }

        /// <summary>
        /// Is raised when this PluginFactory is disposed 
        /// </summary>
        public event EventHandler Disposed;

        public void Start()
        {
        }

        public IEnumerable<T> FilterPlugins<T>(Func<T, bool> filter) where T : class
        {
            return Array.Empty<T>();
        }

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

        public void LoadDynamics()
        {
        }
    }
}
