
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.ParallelProcessing
{
    public abstract class TaskWorkerBase : ITaskWorker
    {
        /// <summary>
        /// Gets the current Task that is being processed by this TaskWorker instance
        /// </summary>
        public virtual ITask CurrentTask { get; protected set; }

        /// <summary>
        /// Processes a specific task
        /// </summary>
        /// <param name="task">a task that was fetched from a priority queue</param>
        public virtual void Process(ITask task)
        {
        }

        /// <summary>
        /// Provides the opportunity to implement an idle - scenario for your taskworker. Implement this, if your Taskworker dies when it is idle for too long.
        /// </summary>
        public virtual void Idle()
        {
        }

        /// <summary>
        /// Resets this worker to its initial state
        /// </summary>
        public virtual void Reset()
        {
        }

        /// <summary>
        /// Quits this worker. If the worker is not shared between processors, this method should call also call Dispose
        /// </summary>
        public virtual void Quit()
        {
        }

        /// <summary>
        /// Processes the given Task async
        /// </summary>
        /// <param name="task">the ITask object to process</param>
        /// <returns>an async awaitable task object</returns>
        public virtual async Task ProcessAsync(ITask task)
        {
            Process(task);
        }

        /// <summary>Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.</summary>
        public virtual void Dispose()
        {
        }
    }
}
