using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Formatting.PluginSystemExtensions.Configuration;
using ITVComponents.Settings;
using ITVComponents.Settings.Native;

namespace ITVComponents.Formatting.PluginSystemExtensions
{
    [Serializable]
    public class PluginConstSection:JsonSettingsSection
    {
        public bool UseExtConfig { get; set; } = false;

        private static PluginConstSection Instance => GetSection<PluginConstSection>("ITVPlugins_Constants");

        public ParameterConfigurationCollection Parameters { get; set; } = new ParameterConfigurationCollection();

        public static class Helper
        {
            private static PluginConstSettings native = NativeSettings.GetSection<PluginConstSettings>("ITVenture:Plugins:Constants");
            public static ParameterConfigurationCollection Parameters => Instance.UseExtConfig ? Instance.Parameters : native.Parameters;
        }
    }
}
