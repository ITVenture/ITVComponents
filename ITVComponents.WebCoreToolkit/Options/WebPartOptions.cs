using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.AspExtensions.Options;

namespace ITVComponents.WebCoreToolkit.Options
{
    public class WebPartOptions
    {
        public List<AssemblyPartConfiguration> Assemblies { get; set; } = new();
    }
}
