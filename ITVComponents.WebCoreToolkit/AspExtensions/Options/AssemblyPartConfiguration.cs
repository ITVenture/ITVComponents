using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.SettingsExtensions;

namespace ITVComponents.WebCoreToolkit.AspExtensions.Options
{
    public class AssemblyPartConfiguration
    {
        public string AssemblyName { get; set; }

        public string DetailConfigPath { get; set; }

        [AutoResolveChildren]
        public Dictionary<string,string> DetailConfigPaths { get; set; }
    }
}
