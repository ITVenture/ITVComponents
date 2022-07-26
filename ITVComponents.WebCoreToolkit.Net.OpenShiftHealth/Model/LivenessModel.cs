using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Net.OpenShiftHealth.Model
{
    /// <summary>
    /// Represents the current Live-State of the system
    /// </summary>
    public class LivenessModel
    {
        /// <summary>
        /// The Current state of the System
        /// </summary>
        public UpState Live { get; set; }
    }
}
