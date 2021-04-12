using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.InterProcessCommunication.Shared.Base;
using ITVComponents.Settings;

namespace ITVComponents.InterProcessCommunication.ManagementExtensions.Configuration
{
    public class ConfigurationClient:ConfiguratorBase
    {
        /// <summary>
        /// the client that enables this ConfigurationClient object to communicate with the target process
        /// </summary>
        private IBaseClient remoteClient;

        /// <summary>
        /// the name of the configuration server in the attached process
        /// </summary>
        private string configServerName;

        /// <summary>
        /// The server object that is used to exchange settings
        /// </summary>
        private IConfigurationServer serverObject;
        /// <summary>
        /// Initializes a new instance of the ConfigurationClient class
        /// </summary>
        /// <param name="remoteClient">the clienObject that enables the communication with the target process</param>
        /// <param name="configServerName">the name of the ConfigurationServer object</param>

        public ConfigurationClient(IBaseClient remoteClient, string configServerName) : base()
        {
            this.remoteClient = remoteClient;
            this.configServerName = configServerName;
        }

        /// <summary>
        /// Gets an array of available Configuration Names on the current application
        /// </summary>
        public override string[] ConfigurationNames => Server.ConfigurationNames;

        /// <summary>
        /// Reads a specific JsonSettingsSection
        /// </summary>
        /// <param name="name">the name of the Configuration Section</param>
        /// <returns>the value of the requested SettingsSection</returns>
        public override JsonSettingsSection GetSetting(string name)
        {
            return Server.GetSetting(name);
        }

        /// <summary>
        /// Writes a specific JsonSettingsSection
        /// </summary>
        /// <param name="name">the name of the ConfigurationSection</param>
        /// <param name="section">the new value to be written to the target-configuration</param>
        public override void UpdateSetting(string name, JsonSettingsSection section)
        {
            Server.UpdateSetting(name,section);
        }

        /// <summary>
        /// Gets the connected ConfigurationServer object
        /// </summary>
        private IConfigurationServer Server
        {
            get
            {
                if (serverObject == null)
                {
                    serverObject = remoteClient.CreateProxy<IConfigurationServer>(configServerName);
                }

                return serverObject;
            }
        }
    }
}
