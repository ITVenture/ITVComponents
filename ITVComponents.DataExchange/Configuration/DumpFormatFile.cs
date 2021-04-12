using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ITVComponents.DataExchange.Configuration
{
    [Serializable]
    public class DumpFormatFile
    {
        /// <summary>
        /// Gets or sets the name for this Formatfile config
        /// </summary>
        public string ConfigName { get; set; }

        /// <summary>
        /// The Format-File that holds the hint that is used to format the content of this configuration
        /// </summary>
        public string FormatFile { get; set; }

        /// <summary>
        /// Gets or sets the mode that is used to get the real content of the file
        /// </summary>
        public DumpFormatFileMode FileMode { get; set; }

        /// <summary>
        /// The Child-configurations that must be applied to each record of the current level
        /// </summary>
        public DumpConfigurationCollection Children { get; set; } = new DumpConfigurationCollection();

        /// <summary>Returns a string that represents the current object.</summary>
        /// <returns>A string that represents the current object.</returns>
        /// <filterpriority>2</filterpriority>
        public override string ToString()
        {
            if (!string.IsNullOrEmpty(FormatFile))
            {
                return $"{ConfigName} ({FileMode}) ({Children.Count} children)";
            }

            return base.ToString();
        }
    }

    public enum DumpFormatFileMode
    {
        Direct,
        File
    }
}
