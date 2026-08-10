using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Logging;
using ITVComponents.Plugins.Collections;
using ITVComponents.Plugins.Helpers;
using ITVComponents.Plugins.Initialization;
using ITVComponents.Plugins.PluginServices;

namespace ITVComponents.Plugins.Scoping
{
    internal class PluginScope:IPluginFactory
    {
        private readonly PluginFactory parent;
        private readonly PluginCollector plugins;
        private bool closed = false;
        public PluginScope(PluginFactory parent, PluginCollector plugins)
        {
            this.parent = parent;
            this.plugins = plugins;
        }

        /// <summary>
        /// True when this scope is a transient loading-scope (as opposed to an explicit operation-scope). Passed
        /// through from the collector - the factory uses it in HasActiveScope to tell an explicit scope (always
        /// binding) from a transient loading-scope (binding only while a transient load is running).
        /// </summary>
        internal bool IsTransientLoadScope => plugins.IsTransientLoadScope;

        public void Dispose()
        {
            if (!closed)
            {
                if (plugins.IsTransientLoadScope)
                {
                    throw new InvalidOperationException("Call Close instead of dispose for transient loading contexts");
                }

                ScopeClose();
            }

            OnDisposed();
        }

        public IPlugin[] ScopeClose()
        {
            try
            {
                return parent.CloseScope(this);
            }
            finally
            {
                closed = true;
            }
        }

        public IEnumerator<IPlugin> GetEnumerator()
        {
            return plugins.Plugins.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <summary>
        /// As with the factory, an empty name is simply "no plugin" and yields null. Passed through unfiltered it
        /// would come back as an ArgumentNullException out of the ConcurrentDictionary - a stacktrace that says
        /// nothing about the actual cause.
        /// </summary>
        public IPlugin this[string pluginName] =>
            !string.IsNullOrEmpty(pluginName) ? plugins[pluginName] : null;

        public IPlugin this[string pluginName, bool triggerAsParameterRequest, PluginRef callingPluginRef]
        {
            get
            {
                var retVal = this[pluginName];
                if (retVal == null && triggerAsParameterRequest)
                {
                    retVal = parent.WithScope(this,
                        s => parent[pluginName, true, callingPluginRef]);
                }

                return retVal;
            }
        }

        public IPlugin this[string pluginName, bool triggerAsParameterRequest]
        {
            get
            {
                var retVal = this[pluginName];
                if (retVal == null && triggerAsParameterRequest)
                {
                    retVal = parent.WithScope(this,
                        s => parent[pluginName, true]);
                }

                return retVal;
            }
        }

        internal StringFormatProvider Formatter { get; private set; }

        public T LoadPlugin<T>(string uniqueName, string pluginConstructor) where T : class, IPlugin
        {
            return parent.WithScope(this, s => parent.LoadPlugin<T>(uniqueName, pluginConstructor));
        }

        /// <summary>
        /// Like the 2-arg overload: run the load in THIS scope (WithScope sets the factory's CurrentScope), so a
        /// plugin marked as transient really ends up in this (transient) loading-scope - instead of slipping past
        /// it into the factory-wide collection, which is why the transient mode used to be a no-op.
        /// </summary>
        public T LoadPlugin<T>(string uniqueName, string pluginConstructor, Dictionary<string, object> customVariables, bool? doBuffer = null) where T : class, IPlugin
        {
            return parent.WithScope(this, s => parent.LoadPlugin<T>(uniqueName, pluginConstructor, customVariables, doBuffer));
        }

        public T LoadPlugin<T>(string uniqueName, string pluginConstructor, bool buffer) where T : class, IPlugin
        {
            return parent.WithScope(this, s => parent.LoadPlugin<T>(uniqueName, pluginConstructor, buffer));
        }

        /// <summary>
        /// All plugins of the requested type that are reachable from this scope - the ones owned by the scope plus
        /// the ones of the factory it was opened on (the collector chains to its parent).
        /// </summary>
        public IEnumerable<T> GetPlugins<T>() where T : class, IPlugin
        {
            foreach (var plugin in plugins.Plugins)
            {
                if (plugin is T typed)
                {
                    yield return typed;
                }
            }
        }

        public T GetPlugin<T>() where T : class, IPlugin
        {
            return GetPlugins<T>().FirstOrDefault();
        }

        internal void SetFormatter(StringFormatProvider prov)
        {
            if (Formatter != null)
            {
                LogEnvironment.LogDebugEvent(
                    $"There already is an instance loaded for String-formatting ({Formatter.UniqueName}). This instance ({prov.UniqueName}) is being ignored.",
                    LogSeverity.Warning);

            }
            else
            {
                Formatter = prov;
            }
        }

        public event EventHandler Disposed;

        protected virtual void OnDisposed()
        {
            Disposed?.Invoke(this, EventArgs.Empty);
        }
    }
}
