using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ITVComponents.ParallelProcessing
{
    /// <summary>
    /// EventArguments that contain Integration information about a task
    /// </summary>
    public class IntegrationEventArgs : EventArgs
    {
        /// <summary>
        /// the task that may require integration work before it can be processed
        /// </summary>
        private ITask task;

        /// <summary>
        /// Initializes a new instance of the IntegrationEventArgs class
        /// </summary>
        /// <param name="task">the task that may require integration work before it can be processed</param>
        public IntegrationEventArgs(ITask task)
        {
            this.task = task;
        }

        /// <summary>
        /// Gets the task that is about to be integrated into the runtime environment
        /// </summary>
        public ITask Task
        {
            get { return task; }
        }
    }
}
