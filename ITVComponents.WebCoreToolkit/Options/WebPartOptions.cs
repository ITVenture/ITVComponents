using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.SettingsExtensions;
using ITVComponents.WebCoreToolkit.AspExtensions.Options;

namespace ITVComponents.WebCoreToolkit.Options
{
    public class WebPartOptions
    {
        [AutoResolveChildren]
        public List<AssemblyPartConfiguration> Assemblies { get; set; } = new();
    }
}
