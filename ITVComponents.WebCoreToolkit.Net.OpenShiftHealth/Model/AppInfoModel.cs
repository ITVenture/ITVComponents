using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Net.OpenShiftHealth.Model
{
    /// <summary>
    /// Describes the current Application
    /// </summary>
    public class AppInfoModel
    {
        /// <summary>
        /// The name of the current application
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// A Brief description of the system
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// The Build-Version currently running
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// The Timestamp, when this info was created
        /// </summary>
        public DateTime TimeStamp { get; set; }

        /// <summary>
        /// The System-State of this application
        /// </summary>
        public UpState State { get; set; }
        
        /// <summary>
        /// A Set of performet checks and their results
        /// </summary>
        public IList<Check> Checks { get; set; } = new List<Check>();
    }
}
