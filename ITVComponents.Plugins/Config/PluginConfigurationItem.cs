using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ITVComponents.Plugins.Config
{
    /// <summary>
    /// Holds the construction Instructions for a single plugin instance
    /// </summary>
    [Serializable]
    public class PluginConfigurationItem
    {
        /// <summary>
        /// Gets or sets the name of this item
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the construction instruction for the plugin instance
        /// </summary>
        public string ConstructionString { get; set; }
        
        /// <summary>
        /// Gets or sets a value indicating whether a specific plugin is disabled
        /// </summary>
        public bool Disabled { get;set; }

        /// <summary>Returns a string that represents the current object.</summary>
        /// <returns>A string that represents the current object.</returns>
        /// <filterpriority>2</filterpriority>
        public override string ToString()
        {
            if (!string.IsNullOrEmpty(Name))
            {
                return $"{(Disabled?"--DISABLED--":"")}{Name} ({ConstructionString})";
            }

            return base.ToString();
        }
    }
}
