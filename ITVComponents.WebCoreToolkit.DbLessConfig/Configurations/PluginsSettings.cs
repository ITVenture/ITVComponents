using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Models;

namespace ITVComponents.WebCoreToolkit.DbLessConfig.Configurations
{
    public class PluginsSettings
    {
        public const string SettingsKey="ITVenture:WebPlugins";

        public WebPlugin[] WebPlugins { get; set; }

        public Dictionary<string, WebPluginGenericParam[]> GenericParameters { get; set; }
    }
}
