using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Plugins.Config;

namespace ITVComponents.Plugins.DatabaseDrivenConfiguration.Helpers
{
    public class PluginConfigItem
    {
        public PluginConfigurationItem PluginDefinition { get; set; }

        public List<GenericTypeDefinition> GenericArguments { get; set; }
    }
}
