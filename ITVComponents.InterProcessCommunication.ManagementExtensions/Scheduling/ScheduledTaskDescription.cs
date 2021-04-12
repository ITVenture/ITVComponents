using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ITVComponents.InterProcessCommunication.ManagementExtensions.Scheduling
{
    /// <summary>
    /// Describes a scheduled Task
    /// </summary>
    [Serializable]
    public class ScheduledTaskDescription
    {
        /// <summary>
        /// Gets or sets a description of the current Task
        /// </summary>
        public string TaskDescription { get; set; }

        /// <summary>
        /// Gets the MetaData information for a scheduled task
        /// </summary>
        public Dictionary<string, object> MetaData { get; set; } 

        /// <summary>
        /// Gets or sets the last Execution of this Task
        /// </summary>
        public DateTime LastExecution { get; set; }

        /// <summary>
        /// Gets or sets remarks that may be relevant to the user requesting the Description of a task
        /// </summary>
        public string Remarks { get; set; }

        /// <summary>
        /// Gets or sets the RequestId of this request. this id can be used to trigger the immediate scheduling of the given task
        /// </summary>
        public string RequestId { get; set; }
    }
}
