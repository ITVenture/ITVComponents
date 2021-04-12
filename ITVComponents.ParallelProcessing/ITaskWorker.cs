using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.ParallelProcessing
{
    /// <summary>
    /// A TaskWorker that is capable for processing a specific Task
    /// </summary>
    public interface ITaskWorker: IDisposable
    {
        /// <summary>
        /// Gets the current Task that is being processed by this TaskWorker instance
        /// </summary>
        ITask CurrentTask { get; }

        /// <summary>
        /// Processes a specific task
        /// </summary>
        /// <param name="task">a task that was fetched from a priority queue</param>
        void Process(ITask task);

        /// <summary>
        /// Provides the opportunity to implement an idle - scenario for your taskworker. Implement this, if your Taskworker dies when it is idle for too long.
        /// </summary>
        void Idle();

        /// <summary>
        /// Resets this worker to its initial state
        /// </summary>
        void Reset();

        /// <summary>
        /// Quits this worker. If the worker is not shared between processors, this method should call also call Dispose
        /// </summary>
        void Quit();
    }
}
