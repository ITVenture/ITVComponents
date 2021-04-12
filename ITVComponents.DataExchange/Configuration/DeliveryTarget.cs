using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ITVComponents.DataExchange.Configuration
{
    [Serializable]
    public class DeliveryTarget
    {
        /// <summary>
        /// Gets or sets the Name of this delivery Target
        /// </summary>
        public string TargetName { get; set; }

        /// <summary>
        /// Gets or sets the name under which to register a queries result on the specified target
        /// </summary>
        public string RegisterName { get; set; }


        /// <summary>Returns a string that represents the current object.</summary>
        /// <returns>A string that represents the current object.</returns>
        /// <filterpriority>2</filterpriority>
        public override string ToString()
        {
            if (!string.IsNullOrEmpty(TargetName))
            {
                return $"{TargetName} ({RegisterName})";
            }

            return base.ToString();
        }
    }
}
