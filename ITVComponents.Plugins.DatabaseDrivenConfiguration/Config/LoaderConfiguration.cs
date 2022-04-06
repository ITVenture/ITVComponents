using System;

namespace ITVComponents.Plugins.DatabaseDrivenConfiguration.Config
{
    /// <summary>
    /// Holds the construction Instructions for a single plugin instance
    /// </summary>
    [Serializable]
    public class LoaderConfiguration
    {
        /// <summary>
        /// Gets or sets the name of this item
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the construction instruction for the plugin instance
        /// </summary>
        public string PluginTableName { get; set; }

        /// <summary>
        /// Gets or sets the Table-Name that contains generic arguments
        /// </summary>
        public string ParamTableName { get; set; }

        /// <summary>
        /// Gets or sets the construction instruction for the plugin instance
        /// </summary>
        public int RefreshCycle { get; set; }

        /// <summary>Returns a string that represents the current object.</summary>
        /// <returns>A string that represents the current object.</returns>
        /// <filterpriority>2</filterpriority>
        public override string ToString()
        {
            if (!string.IsNullOrEmpty(Name))
            {
                return $"{Name} ({PluginTableName}, {RefreshCycle} milliseconds)";
            }

            return base.ToString();
        }
    }
}
