using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.Plugins;
using ITVComponents.Plugins.Initialization;
using ITVComponents.Settings;

namespace ITVComponents.Formatting.PluginSystemExtensions
{
    public class PluginParameterFormatter:IStringFormatProvider,IConfigurablePlugin

    {
        /// <summary>
        /// Holds the constants for the containing pluginFactory instance
        /// </summary>
        private Dictionary<string, object> formatPrototype = new Dictionary<string, object>();

        /// <summary>
        /// Locks the format-object while it is rebuilt
        /// </summary>
        private ManualResetEvent configLock = new ManualResetEvent(true);

        /// <summary>
        /// Processes a raw-string and uses it as format-string of the configured const-collection
        /// </summary>
        /// <param name="rawString">the raw-string that was read from a plugin-configuration string</param>
        /// <returns>the format-result of the raw-string</returns>
        public string ProcessLiteral(string rawString)
        {
            configLock.WaitOne();
            return formatPrototype.FormatText(rawString);
        }

        /// <summary>
        /// Gets or sets the UniqueName of this Plugin
        /// </summary>
        public string UniqueName { get; set; }

        /// <summary>
        /// Gets a list of consumed sections by the implementing component
        /// </summary>
        public JsonSectionDefinition[] ConsumedSections { get; }

        /// <summary>
        /// Gets a value indicating whether the component is currently in the configuration mode
        /// </summary>
        public bool Configuring { get; private set; }

        /// <summary>
        /// Indicates whether this deferrable init-object is already initialized
        /// </summary>
        public bool Initialized { get; private set; }

        /// <summary>
        /// Indicates whether this Object requires immediate Initialization right after calling the constructor
        /// </summary>
        public bool ForceImmediateInitialization { get; } = true;

        /// <summary>
        /// Suspends all tasks executed by this component and waits for new settings
        /// </summary>
        public void EnterConfigurationMode()
        {
            configLock.Reset();
            Configuring = true;
        }

        /// <summary>
        /// Resumes all tasks, after the new configuration settings have been applied
        /// </summary>
        public void LeaveConfigurationMode()
        {
            try
            {
                Configuring = false;
                LoadConfig();
            }
            finally
            {
                configLock.Set();
            }
        }

        /// <summary>
        /// Initializes this deferred initializable object
        /// </summary>
        public void Initialize()
        {
            Initialized = true;
        }

        /// <summary>
        /// Instructs the Plugin to read the JsonSettings or to create a default instance if none is available
        /// </summary>
        public void ReadSettings()
        {
            LoadConfig();
        }

        /// <summary>
        ///   Führt anwendungsspezifische Aufgaben durch, die mit der Freigabe, der Zurückgabe oder dem Zurücksetzen von nicht verwalteten Ressourcen zusammenhängen.
        /// </summary>
        public void Dispose()
        {
            configLock.Dispose();
            formatPrototype.Clear();
            OnDisposed();
        }

        /// <summary>
        /// Raises the Disposed event
        /// </summary>
        protected virtual void OnDisposed()
        {
            Disposed?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Loads the configuration for the const-values
        /// </summary>
        private void LoadConfig()
        {
            formatPrototype.Clear();
            foreach (var item in from t in PluginConstSection.Helper.Parameters select new {t.ConstIdentifier,t.ConstValue})
            {
                formatPrototype.Add(item.ConstIdentifier, item.ConstValue);
            }
        }

        /// <summary>
        /// Informs a calling class of a Disposal of this Instance
        /// </summary>
        public event EventHandler Disposed;
    }
}
