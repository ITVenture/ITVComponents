using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Net.OpenShiftHealth.Model
{
    /// <summary>
    /// Represents a single check that was performed on the system
    /// </summary>
    public class Check
    {
        /// <summary>
        /// The name of the performed Check
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The outcome of the Check
        /// </summary>
        public UpState Status { get; set; }
    }
}
