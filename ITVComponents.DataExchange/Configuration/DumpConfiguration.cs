using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ITVComponents.DataExchange.Configuration
{
    [Serializable]
    public class DumpConfiguration
    {
        /// <summary>
        /// The name of this DumpConfiguration
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// the source that contains the items of this DumpConfiguration
        /// </summary>
        public string Source { get; set; }

        /// <summary>
        /// Gets constants that apply for the current dump-configuration
        /// </summary>
        public ConstConfigurationCollection Constants { get; set; } = new ConstConfigurationCollection();

        /// <summary>
        /// Gets a list of format files that contain information how to format a messagepart
        /// </summary>
        public DumpFormatFileCollection Files { get;set; } = new DumpFormatFileCollection();

        /// <summary>Returns a string that represents the current object.</summary>
        /// <returns>A string that represents the current object.</returns>
        /// <filterpriority>2</filterpriority>
        public override string ToString()
        {
            if (!string.IsNullOrEmpty(Name))
            {
                return $"{Name} (@{Source}, {Constants.Count} constants, {Files.Count} files)";
            }

            return base.ToString();
        }

    }
}
