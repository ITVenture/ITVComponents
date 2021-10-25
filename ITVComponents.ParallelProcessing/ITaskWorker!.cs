using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.ParallelProcessing
{
    /// <summary>
    /// Taskworker interface defining a worker that takes a specific type of Tasks
    /// </summary>
    /// <typeparam name="TTask">the specific task implementation to be processed by a worker</typeparam>
    public interface ITaskWorker<TTask> : ITaskWorker where TTask : ITask
    {

        /// <summary>
        /// Gets the current Task that is being processed by this TaskWorker instance
        /// </summary>
        TTask CurrentTask { get; }

        /// <summary>
        /// Processes a specific task
        /// </summary>
        /// <param name="task">a task that was fetched from a priority queue</param>
        void Process(TTask task);

        /// <summary>
        /// Processes the given Task async
        /// </summary>
        /// <param name="task">the ITask object to process</param>
        /// <returns>an async awaitable task object</returns>
        Task ProcessAsync(TTask task);
    }
}
