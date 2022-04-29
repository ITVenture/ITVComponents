using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.AspExtensions.Options
{
    public class AssemblyPartConfiguration
    {
        public string AssemblyName { get; set; }

        public string DetailConfigPath { get; set; }

        public Dictionary<string,string> DetailConfigPaths { get; set; }
    }
}
