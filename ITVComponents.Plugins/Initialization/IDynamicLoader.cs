using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ITVComponents.Plugins.Initialization
{
    public interface IDynamicLoader : IPlugin
    {
        /// <summary>
        /// Loads dynamic assemblies that are required for a specific application
        /// </summary>
        IEnumerable<string> LoadDynamicAssemblies();
    }
}
