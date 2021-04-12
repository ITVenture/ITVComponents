using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Plugins.DatabaseDrivenConfiguration.Config;
using ITVComponents.Settings;
using ITVComponents.Settings.Native;

namespace ITVComponents.Plugins.DatabaseDrivenConfiguration
{
    [Serializable]
    public class DatabaseLoaderConfig:JsonSettingsSection
    {
        /// <summary>
        /// Gets or sets a value indicating whether to use this Configuration object. If the value is set to false, the old .net Settings environment is used.
        /// </summary>
        public bool UseExtConfig { get; set; } = false;

        /// <summary>
        /// Gets or sets the available Loader-Configurations for the databasedriven PluginLoader
        /// </summary>
        public LoaderConfigurationCollection LoaderConfigurations { get; set; } = new LoaderConfigurationCollection();

        /// <summary>
        /// Offers a derived class to define default-configuration-settings
        /// </summary>
        protected override void LoadDefaults()
        {
            LoaderConfigurations.Clear();
            LoaderConfigurations.AddRange(Helper.nativeSection.LoaderConfigurations);
        }

        public static class Helper
        {
            internal static DatabaseLoaderSettings nativeSection = NativeSettings.GetSection<DatabaseLoaderSettings>("ITVenture:Plugins:DatabaseLoaderSettings", d =>
            {
                d.LoaderConfigurations.Add(new LoaderConfiguration
                {
                    Name = "Default",
                    PluginTableName = "Plugins",
                    RefreshCycle = 2000
                });
            });

            private static DatabaseLoaderConfig Section => GetSection<DatabaseLoaderConfig>("ITVComponents_Plugins_DatabaseDrivenConfiguration_DatabaseLoaderConfiguration");

            public static LoaderConfigurationCollection LoaderConfigurations => Section.UseExtConfig ? Section.LoaderConfigurations: nativeSection.LoaderConfigurations;
        }
    }
}
