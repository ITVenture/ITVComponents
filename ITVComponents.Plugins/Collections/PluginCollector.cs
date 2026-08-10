using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.Logging;
using ITVComponents.Plugins.Initialization;

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

        public IDynamicLoader[] DynamicLoaders =>
            (from t in plugins where t.Value is IDynamicLoader select (IDynamicLoader)t.Value).ToArray();

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
                if (retVal)
                {
                    // Whoever HOLDS the plugin cleans it up. This deliberately hangs here and not on the factory:
                    // the factory resolves its collection through the scope that is active at dispose-time - a
                    // plugin living in a scope would be looked for where it never was.
                    pi.Disposed += PluginDisposed;
                }
            }

            return retVal;
        }

        public bool TryRemove(string srcUniqueName, out IPlugin tmp)
        {
            bool removed = plugins.TryRemove(srcUniqueName, out tmp);
            if (removed && tmp != null)
            {
                tmp.Disposed -= PluginDisposed;
            }

            return removed;
        }

        /// <summary>
        /// Takes a disposed plugin out of EXACTLY THIS collection. Matching by instance is necessary because a
        /// different plugin may meanwhile sit under the same name (re-load); that new entry has to stay.
        /// </summary>
        private void PluginDisposed(object sender, EventArgs e)
        {
            if (sender is not IPlugin src)
            {
                return;
            }

            src.Disposed -= PluginDisposed;
            string name = src.UniqueName;
            if (string.IsNullOrEmpty(name))
            {
                // Without a name the entry is only findable by instance - rare (a plugin dying before it was
                // named), but it must not be left behind either.
                foreach (KeyValuePair<string, IPlugin> entry in plugins)
                {
                    if (ReferenceEquals(entry.Value, src))
                    {
                        plugins.TryRemove(entry.Key, out _);
                        break;
                    }
                }

                return;
            }

            if (plugins.TryGetValue(name, out IPlugin current) && ReferenceEquals(current, src))
            {
                plugins.TryRemove(name, out _);
            }
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
                    IStoppable plugin = pluginArray[i] as IStoppable;
                    if (plugin != null)
                    {
                        plugin.Stop();
                    }
                }

                for (int i = pluginArray.Length - 1; i >= 0; i--)
                {
                    IPlugin pi = pluginArray[i];
                    try
                    {
                        pi.Dispose();
                    }
                    catch (Exception ex)
                    {
                        LogEnvironment.LogEvent(ex.ToString(), LogSeverity.Error, "PluginSystem");
                    }
                }
            }

            plugins.Clear();

            return pluginArray;
        }

        public void TryAddRegisteredObject(string parameterName, object instance)
        {
            registeredObjects.TryAdd(parameterName, instance);
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
