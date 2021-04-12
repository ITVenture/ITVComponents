using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.InterProcessCommunication.Shared.Base;
using ITVComponents.Plugins;
using ITVComponents.Settings;

namespace ITVComponents.InterProcessCommunication.ManagementExtensions.Configuration
{
    public abstract class ConfiguratorBase:IPlugin, IDeferredInit, IConfigurationServer
    {
        /// <summary>
        /// Holds a list of available configurators
        /// </summary>
        private static ConcurrentDictionary<string,ConfiguratorBase> availableConfigurators = new ConcurrentDictionary<string, ConfiguratorBase>();

        public static string[] KnownConfigurators => availableConfigurators.Keys.ToArray();

        /// <summary>
        /// Initializes a new instance of the ConfiguratorBase class
        /// </summary>
        protected ConfiguratorBase()
        {
        }

        /// <summary>
        /// Gets or sets the UniqueName of this Plugin
        /// </summary>
        public string UniqueName { get; set; }

        /// <summary>
        /// Indicates whether this deferrable init-object is already initialized
        /// </summary>
        public bool Initialized { get; private set; }

        /// <summary>
        /// Gets an array of available Configuration Names on the current application
        /// </summary>
        public abstract string[] ConfigurationNames { get; }

        /// <summary>
        /// Indicates whether this Object requires immediate Initialization right after calling the constructor
        /// </summary>
        public bool ForceImmediateInitialization { get; } = true;

        /// <summary>
        /// Gets the requested configurator from the list of available configurators
        /// </summary>
        /// <param name="uniqueName">the unique name of the desired configurator</param>
        /// <returns>the desired configurator object</returns>
        public static ConfiguratorBase GetConfigurator(string uniqueName)
        {
            if (availableConfigurators.TryGetValue(uniqueName, out var configurator))
            {
                return configurator;
            }

            return null;
        }

        /// <summary>
        /// Reads a specific JsonSettingsSection
        /// </summary>
        /// <param name="name">the name of the Configuration Section</param>
        /// <returns>the value of the requested SettingsSection</returns>
        public abstract JsonSettingsSection GetSetting(string name);

        /// <summary>
        /// Writes a specific JsonSettingsSection
        /// </summary>
        /// <param name="name">the name of the ConfigurationSection</param>
        /// <param name="section">the new value to be written to the target-configuration</param>
        public abstract void UpdateSetting(string name, JsonSettingsSection section);

        /// <summary>
        /// Initializes this deferred initializable object
        /// </summary>
        public void Initialize()
        {
            try
            {
                InitializeConfigurator();
            }
            finally
            {
                availableConfigurators.AddOrUpdate(UniqueName, this, (s, o) => this);
                Initialized = true;
            }
        }

        /// <summary>Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.</summary>
        /// <filterpriority>2</filterpriority>
        public void Dispose()
        {
            OnDisposed();
        }

        /// <summary>
        /// Raises the Disposed Event
        /// </summary>
        protected virtual void OnDisposed()
        {
            availableConfigurators.TryRemove(UniqueName, out var tmp);
            Disposed?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Enables derived objects to perform custom initialization tasks
        /// </summary>
        protected virtual void InitializeConfigurator()
        {
        }

        /// <summary>
        /// Informs a calling class of a Disposal of this Instance
        /// </summary>
        public event EventHandler Disposed;

    }
}
