using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ITVComponents.Plugins.PluginServices
{
    [Serializable]
    public class PluginConstructionElement
    {
        /// <summary>
        /// Gets or sets the name of the target assembly
        /// </summary>
        public string AssemblyName { get; set; }

        /// <summary>
        /// Gets or sets the Type-Name of the target assembly
        /// </summary>
        public string TypeName { get; set; }

        /// <summary>
        /// Gets or sets the Parameters that can be used 
        /// </summary>
        public PluginParameterElement[] Parameters { get; set; }
    }
}
