using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ITVComponents.DataExchange.TextImport.Config
{
    [Serializable]
    public class RegexConfiguration
    {
        /// <summary>
        /// Gets or sets the required regex-options for this Regex-Configuration
        /// </summary>
        public RegexOptions Options { get; set; }

        /// <summary>
        /// The RegularExpression that is Configured with this item
        /// </summary>
        public string Expression { get; set; }

        /// <summary>
        /// Indicates that this Regex is a Start-Trigger for a new item
        /// </summary>
        public bool StartsNewItem { get; set; }

        /// <summary>
        /// Indicates that this Regex is a Close-Trigger for an existing item
        /// </summary>
        public bool EndsCurrentItem { get; set; }
    }
}
