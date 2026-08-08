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
        /// True, wenn dieser Scope ein transienter Ladescope ist (kein expliziter Operations-/Fresh-Scope).
        /// Aus dem Collector durchgereicht - die Factory unterscheidet damit in <c>HasActiveScope</c>, ob ein
        /// expliziter Scope (immer verbindlich) oder ein transienter Ladescope (nur beim Transient-Load) aktiv ist.
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
        /// Wie bei der Factory ist ein leerer Name schlicht "kein Plugin" und liefert null. Ungefiltert
        /// weitergereicht kaeme er als ArgumentNullException aus der ConcurrentDictionary zurueck - ein
        /// Stacktrace, der nichts ueber die eigentliche Ursache sagt.
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

        public T LoadPlugin<T>(string uniqueName, string pluginConstructor, Dictionary<string, object> customVariables, bool? doBuffer = null) where T : class, IPlugin
        {
            // Wie die 2-arg-Variante: den Load in DIESEM Scope ausfuehren (WithScope setzt CurrentScope), damit
            // ein transient markiertes Plugin tatsaechlich in diesen (transienten) Ladescope geladen wird - statt
            // wie bisher an ihm vorbei in pluginInstances (der Grund, warum der Transient-Modus bisher ein No-op war).
            return parent.WithScope(this, s => parent.LoadPlugin<T>(uniqueName, pluginConstructor, customVariables, doBuffer));
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
