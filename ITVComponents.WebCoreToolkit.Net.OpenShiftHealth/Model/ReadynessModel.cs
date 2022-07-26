using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Net.OpenShiftHealth.Model
{
    /// <summary>
    /// Represents the current Ready-State of the system
    /// </summary>
    public class ReadynessModel
    {
        /// <summary>
        /// The Current state of the System
        /// </summary>
        public UpState Ready { get; set; }
    }
}
