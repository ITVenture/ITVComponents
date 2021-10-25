using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.ParallelProcessing
{
    public class TaskWorkerBase<TTask> : TaskWorkerBase, ITaskWorker<TTask> where TTask : ITask
    {
        /// <summary>
        /// Gets the current Task that is being processed by this TaskWorker instance
        /// </summary>
        TTask ITaskWorker<TTask>.CurrentTask => (TTask)base.CurrentTask;

        /// <summary>
        /// Processes a specific task
        /// </summary>
        /// <param name="task">a task that was fetched from a priority queue</param>
        public override void Process(ITask task)
        {
            Process((TTask)task);
        }

        /// <summary>
        /// Processes a specific task
        /// </summary>
        /// <param name="task">a task that was fetched from a priority queue</param>
        public virtual void Process(TTask task)
        {
        }

        /// <summary>
        /// Processes the given Task async
        /// </summary>
        /// <param name="task">the ITask object to process</param>
        /// <returns>an async awaitable task object</returns>
        public async override Task ProcessAsync(ITask task)
        {
            await ProcessAsync((TTask)task);
        }

        /// <summary>
        /// Processes the given Task async
        /// </summary>
        /// <param name="task">the ITask object to process</param>
        /// <returns>an async awaitable task object</returns>
        public virtual async Task ProcessAsync(TTask task)
        {
            Process(task);
        }
    }
}
