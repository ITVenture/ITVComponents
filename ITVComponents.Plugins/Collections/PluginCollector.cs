using ITVComponents.Helpers;
using ITVComponents.Logging;
using ITVComponents.Plugins.Initialization;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ITVComponents.Plugins.Collections
{
    internal class PluginCollector : IEnumerable<KeyValuePair<string,IPlugin>>
    {
        private readonly PluginCollector parent;
        private ConcurrentDictionary<string, IPlugin> plugins;
        private bool isTransientLoadingScope;
        private ConcurrentDictionary<string, ManualResetEventSlim> pluginInitializationPromises;

        /// <summary>
        /// All objects that can be accessed directly as constructor parameters when an object requests it
        /// </summary>
        private ConcurrentDictionary<string, object> registeredObjects;

        /// <summary>
        /// Names of <see cref="registeredObjects"/> whose lifecycle is owned by THIS scope: they are disposed
        /// (if <see cref="IDisposable"/>) when the scope is closed. By default registered objects out-live the
        /// scope (they are not disposed here) — opt in per object via <see cref="TryAddRegisteredObject(string,object,bool)"/>.
        /// </summary>
        private readonly HashSet<string> scopeOwnedObjects = new HashSet<string>();

        private AsyncLocal<Dictionary<string, object>> localRegistrations;

        public PluginCollector(bool isTransientLoadingScope)
        {
            pluginInitializationPromises = new ConcurrentDictionary<string, ManualResetEventSlim>();
            plugins = new ConcurrentDictionary<string, IPlugin>();
            registeredObjects = new ConcurrentDictionary<string, object>();
            localRegistrations = new AsyncLocal<Dictionary<string, object>>();
            this.isTransientLoadingScope = isTransientLoadingScope;
        }

        public PluginCollector(PluginCollector parent, bool isTransientLoadingScope):this(isTransientLoadingScope)
        {
            this.parent = parent;
        }

        public bool IsTransientLoadScope => isTransientLoadingScope;

        public IPlugin this[string name]
        {
            get
            {
                if (pluginInitializationPromises.TryGetValue(name, out var wh))
                {
                    if (!wh.IsSet)
                    {
                        wh.Wait(5000);
                    }

                    if (plugins.TryGetValue(name, out var pi))
                    {
                        return pi;
                    }
                }

                if (parent != null)
                {
                    return parent[name];
                }

                return null;
            }
        }

        public IDynamicLoader[] DynamicLoaders
        {
            get
            {
                // Include the parent's loaders, just like every other member here traverses the parent.
                // A scope-collector wraps the main factory's collector; scoped-plugin resolution
                // (RequestScopePlugin) reads DynamicLoaders through the scope, so a loader registered on
                // the main factory must be visible from within a scope - otherwise scope[name,true] never
                // finds it.
                IEnumerable<IDynamicLoader> own =
                    from t in plugins where t.Value is IDynamicLoader select (IDynamicLoader)t.Value;
                if (parent != null)
                {
                    own = own.Concat(parent.DynamicLoaders);
                }

                return own.ToArray();
            }
        }

        public string[] Names
        {
            get
            {
                IEnumerable<string> tmp = plugins.Keys;
                if (parent != null)
                {
                    tmp = tmp.Union(parent.Names);
                }

                return tmp.ToArray();
            }
        }

        public IEnumerable<IPlugin> Plugins
        {
            get
            {
                IEnumerable<IPlugin> retVal = plugins.Values;
                if (parent != null)
                {
                    retVal = retVal.Concat(parent.Plugins);
                }

                return retVal;
            }
        }

        public bool TryGetValue(string pluginName, out IPlugin o)
        {
            if (plugins.TryGetValue(pluginName, out o))
            {
                return true;
            }

            if (parent != null)
            {
                return parent.TryGetValue(pluginName, out o);
            }

            return false;
        }

        public bool ContainsKey(string uniqueName)
        {
            if (plugins.ContainsKey(uniqueName))
            {
                return true;
            }

            if (parent != null)
            {
                return parent.ContainsKey(uniqueName);
            }

            return false;
        }

        public IEnumerator<KeyValuePair<string, IPlugin>> GetEnumerator()
        {
            IEnumerable<KeyValuePair<string, IPlugin>> retVal = plugins;
            if (parent != null)
            {
                retVal = retVal.Concat(parent);
            }

            return retVal.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public bool TryAdd(string uniqueName, IPlugin pi)
        {
            bool retVal = parent == null || !parent.ContainsKey(uniqueName);
            if (retVal)
            {
                retVal = plugins.TryAdd(uniqueName, pi);
            }

            return retVal;
        }

        public bool TryRemove(string srcUniqueName, out IPlugin tmp)
        {
            return plugins.TryRemove(srcUniqueName, out tmp);
        }

        public bool TryInitPluginLoad(string uniqueName, out ManualResetEventSlim trigger)
        {
            return this.pluginInitializationPromises.TryAdd(uniqueName, trigger = new ManualResetEventSlim(false));
        }

        public bool TryQuitPluginLoad(string uniqueName, out ManualResetEventSlim wh)
        {
            return pluginInitializationPromises.TryRemove(uniqueName, out wh);
        }

        public IPlugin[] Clear()
        {
            IPlugin[] pluginArray = plugins.Values.ToArray();
            if (!isTransientLoadingScope)
            {
                for (int i = 0; i < pluginArray.Length; i++)
                {
                    if (pluginArray[i] is IStoppable stop)
                    {
                        LogEnvironment.LogDebugEvent($"Stopping {pluginArray[i].UniqueName}", LogSeverity.Report);
                        stop.Stop();
                    }
                }

                for (int i = pluginArray.Length - 1; i >= 0; i--)
                {
                    IPlugin pi = pluginArray[i];
                    try
                    {
                        LogEnvironment.LogDebugEvent($"Calling Dispose on {pi.UniqueName}", LogSeverity.Report);
                        pi.Dispose();
                    }
                    catch (Exception ex)
                    {
                        LogEnvironment.LogEvent($"An Error occurred when disposing the Plugin {pi.UniqueName}: {ex.OutlineException()}.", LogSeverity.Error, "PluginSystem");
                    }
                }

                // Dispose scope-owned registered objects (e.g. per-operation DbContexts). Objects NOT marked
                // owned out-live the scope (default) and are left untouched.
                foreach (var ownedKey in scopeOwnedObjects)
                {
                    if (registeredObjects.TryGetValue(ownedKey, out var owned) && owned is IDisposable disposable)
                    {
                        try
                        {
                            disposable.Dispose();
                        }
                        catch (Exception ex)
                        {
                            LogEnvironment.LogEvent(ex.ToString(), LogSeverity.Error, "PluginSystem");
                        }
                    }
                }

                scopeOwnedObjects.Clear();
            }

            plugins.Clear();

            return pluginArray;
        }

        public void TryAddRegisteredObject(string parameterName, object instance)
        {
            registeredObjects.TryAdd(parameterName, instance);
        }

        /// <summary>
        /// Registers an object as a constructor parameter and, when <paramref name="disposeWithScope"/> is true,
        /// marks it to be disposed (if <see cref="IDisposable"/>) when this scope is closed. Use this for
        /// per-operation dependencies (e.g. a fresh DbContext) that must NOT out-live the scope.
        /// </summary>
        public void TryAddRegisteredObject(string parameterName, object instance, bool disposeWithScope)
        {
            if (registeredObjects.TryAdd(parameterName, instance) && disposeWithScope)
            {
                scopeOwnedObjects.Add(parameterName);
            }
        }

        public void TryAddRegisteredObjectLocal(string parameterName, object instance)
        {
            localRegistrations.Value ??= new Dictionary<string, object>();
            localRegistrations.Value[parameterName] = instance;
        }

        public void ClearLocalRegistrations()
        {
            localRegistrations.Value?.Clear();
            localRegistrations.Value = null;
        }

        public bool IsObjectRegistered(string parameterName)
        {
            bool retVal = registeredObjects.ContainsKey(parameterName);
            if (!retVal && parent != null)
            {
                retVal = parent.IsObjectRegistered(parameterName);
            }

            return retVal;
        }

        public bool IsObjectRegisteredLocal(string parameterName)
        {
            bool retVal = false;
            if (localRegistrations.Value != null)
            {
                retVal = localRegistrations.Value.ContainsKey(parameterName);
            }

            if (!retVal && parent != null)
            {
                retVal = parent.IsObjectRegisteredLocal(parameterName);
            }

            return retVal;
        }

        public object TryGetRegisteredObject(string parameterName)
        {
            object retVal = null;
            if (registeredObjects.ContainsKey(parameterName))
            {
                retVal = registeredObjects[parameterName];
            }

            if (retVal == null && parent != null)
            {
                retVal = parent.TryGetRegisteredObject(parameterName);
            }

            return retVal;
        }

        public object TryGetRegisteredObjectLocal(string parameterName)
        {
            object retVal = null;
            if (localRegistrations.Value != null && localRegistrations.Value.ContainsKey(parameterName))
            {
                retVal = localRegistrations.Value[parameterName];
            }

            if (retVal == null && parent != null)
            {
                retVal = parent.TryGetRegisteredObjectLocal(parameterName);
            }

            return retVal;
        }
    }
}
