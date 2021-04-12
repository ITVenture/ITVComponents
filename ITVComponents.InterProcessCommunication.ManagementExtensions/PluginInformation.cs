using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ITVComponents.InterProcessCommunication.ManagementExtensions
{
    [Serializable]
    public class PluginInformation
    {
        /// <summary>
        /// Gets or sets the PluginName of a specific plugin
        /// </summary>
        public string PluginName { get; set; }

        /// <summary>
        /// Gets or sets the PluginType of a specific plugin
        /// </summary>
        public string PluginType { get; set; }
    }
}
