using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.AssemblyResolving;

namespace ITVComponents
{
    public class ResolverSettings
    {
        public AssemblyResolverConfigurationCollection FixedAssemblies { get; set; } = new AssemblyResolverConfigurationCollection();
    }
}
