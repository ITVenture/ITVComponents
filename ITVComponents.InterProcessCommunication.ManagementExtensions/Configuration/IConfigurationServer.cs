using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Settings;

namespace ITVComponents.InterProcessCommunication.ManagementExtensions.Configuration
{
    /// <summary>
    /// Provides functionality to exchange configuration information
    /// </summary>
    public interface IConfigurationServer
    {
        /// <summary>
        /// Gets an array of available Configuration Names on the current application
        /// </summary>
        string[] ConfigurationNames { get; }

        /// <summary>
        /// Reads a specific JsonSettingsSection
        /// </summary>
        /// <param name="name">the name of the Configuration Section</param>
        /// <returns>the value of the requested SettingsSection</returns>
        JsonSettingsSection GetSetting(string name);

        /// <summary>
        /// Writes a specific JsonSettingsSection
        /// </summary>
        /// <param name="name">the name of the ConfigurationSection</param>
        /// <param name="section">the new value to be written to the target-configuration</param>
        void UpdateSetting(string name, JsonSettingsSection section);
    }
}
