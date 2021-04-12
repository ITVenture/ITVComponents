using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.ParallelProcessing
{
    [Serializable]
    public class TaskContainer
    {
        /// <summary>
        /// Gets or sets the Task that is being scheduled
        /// </summary>
        public ITask Task { get; set; }

        /// <summary>
        /// Gets or sets the Request that has lead to scheduling the provided task
        /// </summary>
        public TaskScheduler.ScheduleRequest Request { get; set; }
    }
}
