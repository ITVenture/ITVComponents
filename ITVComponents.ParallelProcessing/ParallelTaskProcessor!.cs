using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.ParallelProcessing
{
    /// <summary>
    /// Implementation of a Parallel Task processing unit for specific Task Types
    /// </summary>
    /// <typeparam name="TTask">the specific TaskType that can be processed by this processing unit</typeparam>
    public class ParallelTaskProcessor<TTask>:ParallelTaskProcessor where TTask:ITask
    {
        /// <summary>
        /// Initializes a new instance of the ParallelTaskProcesor class
        /// </summary>
        /// <param name="identifier">the unique identifier of this ParallelTaskProcessor instance</param>
        /// <param name="worker">A Callback that generates new Workers on Demand</param>
        /// <param name="highestPriority">the highest priority that is processed by this processor.</param>
        /// <param name="lowestPriority">the highest priority that is processed by this processor.</param>
        /// <param name="workerCount">the number of parallel Workers for this queue</param>
        /// <param name="workerPollTime">the pollTime after that a suspended poller re-checks the queues for more work</param>
        /// <param name="lowTaskThreshold">the minimum number of Items that should be in a queue for permanent processing</param>
        /// <param name="highTaskThreshold">the maximum number of Items that should be queued in a workerQueue</param>
        /// <param name="useAffineThreads">indicates whether to use ThreadAffinity in the workers</param>
        public ParallelTaskProcessor(string identifier, Func<ITaskWorker<TTask>> worker, int highestPriority, int lowestPriority, int workerCount, int workerPollTime, int lowTaskThreshold, int highTaskThreshold, bool useAffineThreads, bool runWithoutSchedulers)
            : base(identifier, worker, highestPriority,lowestPriority, workerCount, workerPollTime, lowTaskThreshold, highTaskThreshold, useAffineThreads, runWithoutSchedulers)
        {
        }

        /// <summary>
        /// Initializes a new instance of the ParallelTaskProcesor class
        /// </summary>
        /// <param name="identifier">the unique identifier of this ParallelTaskProcessor instance</param>
        /// <param name="worker">A Callback that generates new Workers on Demand</param>
        /// <param name="highestPriority">the highest priority that is processed by this processor.</param>
        /// <param name="lowestPriority">the highest priority that is processed by this processor.</param>
        /// <param name="workerCount">the number of parallel Workers for this queue</param>
        /// <param name="workerPollTime">the pollTime after that a suspended poller re-checks the queues for more work</param>
        /// <param name="lowTaskThreshold">the minimum number of Items that should be in a queue for permanent processing</param>
        /// <param name="highTaskThreshold">the maximum number of Items that should be queued in a workerQueue</param>
        /// <param name="useAffineThreads">indicates whether to use ThreadAffinity in the workers</param>
        /// <param name="watchDog">a watchdog instance that will restart worker-instances when they become unresponsive</param>
        public ParallelTaskProcessor(string identifier, Func<ITaskWorker<TTask>> worker, int highestPriority, int lowestPriority, int workerCount, int workerPollTime, int lowTaskThreshold, int highTaskThreshold, bool useAffineThreads, bool runWithoutSchedulers, WatchDog watchDog)
            : base(identifier, worker, highestPriority,lowestPriority, workerCount, workerPollTime, lowTaskThreshold, highTaskThreshold, useAffineThreads, runWithoutSchedulers, watchDog)
        {
        }

        /// <summary>
        /// Enqueues a Task into the appropriate queue
        /// </summary>
        /// <param name="task">the task that requires processing</param>
        public bool EnqueueTask(TTask task)
        {
            return base.EnqueueTask(task);
        }
    }
}
