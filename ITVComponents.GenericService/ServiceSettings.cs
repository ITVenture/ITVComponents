using System.Collections.Generic;
using System.ServiceProcess;
using ITVComponents.Plugins.Config;

namespace ITVComponents.GenericService
{
    public class ServiceSettings
    {
        public string Path { get; set; }
        public string ServiceName { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public ServiceStartMode StartType { get; set; }
        public string ServiceUser { get; set; }
        public string ServicePassword { get; set; }
        public PluginConfigurationCollection PlugIns { get; set; } = new PluginConfigurationCollection();
        public GenericTypeConstructionCollection GenericTypeInformation { get; set; } = new GenericTypeConstructionCollection();
        public List<string> Dependencies { get; set; } = new List<string>();
        public string ServiceStartup { get; set; }
    }
}
