using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ITVComponents.InterProcessCommunication.ManagementExtensions.Scheduling
{
    [Serializable]
    public class SchedulerDescription
    {
        /// <summary>
        /// Gets or sets the Name of the described scheduler
        /// </summary>
        public string SchedulerName { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the given scheduler supports pushing tasks
        /// </summary>
        public bool SupportsPushTask { get; set; }
    }
}
