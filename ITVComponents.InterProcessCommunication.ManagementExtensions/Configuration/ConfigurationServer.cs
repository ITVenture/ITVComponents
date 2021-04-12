using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Settings;

namespace ITVComponents.InterProcessCommunication.ManagementExtensions.Configuration
{
    public class ConfigurationServer:ConfiguratorBase
    {
        /// <summary>
        /// Gets an array of available Configuration Names on the current application
        /// </summary>
        public override string[] ConfigurationNames => JsonSettings.Default.SettingsNames;

        /// <summary>
        /// Reads a specific JsonSettingsSection
        /// </summary>
        /// <param name="name">the name of the Configuration Section</param>
        /// <returns>the value of the requested SettingsSection</returns>
        public override JsonSettingsSection GetSetting(string name)
        {
            return JsonSettings.Default.GetSetting(name);
        }

        /// <summary>
        /// Writes a specific JsonSettingsSection
        /// </summary>
        /// <param name="name">the name of the ConfigurationSection</param>
        /// <param name="section">the new value to be written to the target-configuration</param>
        public override void UpdateSetting(string name, JsonSettingsSection section)
        {
            JsonSettings.Default.ReplaceSetting(name, section);
        }
    }
}
