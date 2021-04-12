using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ITVComponents.ParallelProcessing
{
    /// <summary>
    /// Declares the policy for a scheduler when to run a task
    /// </summary>
    [Serializable]
    public class SchedulerPolicy
    {
        /// <summary>
        /// Gets or sets the name of a Scheduler
        /// </summary>
        public virtual string SchedulerName { get; set; }

        /// <summary>
        /// Gets or sets the scheduler-instruction for the specified scheduler
        /// </summary>
        public virtual string SchedulerInstruction { get; set; }

        /// <summary>Returns a string that represents the current object.</summary>
        /// <returns>A string that represents the current object.</returns>
        /// <filterpriority>2</filterpriority>
        public override string ToString()
        {
            if (!string.IsNullOrEmpty(SchedulerName))
            {
                return $"{SchedulerName} ({SchedulerInstruction})";
            }

            return base.ToString();
        }
    }
}
