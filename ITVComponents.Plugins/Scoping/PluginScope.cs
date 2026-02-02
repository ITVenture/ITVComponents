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

        public IPlugin this[string pluginName]=> plugins[pluginName];

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

        public T LoadPlugin<T>(string uniqueName, string pluginConstructor, Dictionary<string, object> customVariables, bool? doBuffer = null) where T : class, IPlugin
        {
            throw new NotImplementedException();
        }

        public T LoadPlugin<T>(string uniqueName, string pluginConstructor, bool buffer) where T : class, IPlugin
        {
            throw new NotImplementedException();
        }

        public IEnumerable<T> GetPlugins<T>() where T : class, IPlugin
        {
            throw new NotImplementedException();
        }

        public T GetPlugin<T>() where T : class, IPlugin
        {
            throw new NotImplementedException();
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
