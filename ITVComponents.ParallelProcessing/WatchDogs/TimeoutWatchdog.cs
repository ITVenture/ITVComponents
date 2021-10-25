using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Helpers;
using ITVComponents.Logging;

namespace ITVComponents.ParallelProcessing.WatchDogs
{
    public abstract class TimeoutWatchdog:WatchDog
    {
        private readonly int timeout;

        /// <summary>
        /// Initializes a new instance of the TimeoutWatchDog class
        /// </summary>
        /// <param name="timeout"></param>
        protected TimeoutWatchdog(int timeout)
        {
            this.timeout = timeout;
        }

        /// <summary>
        /// Gets or sets a value indicating whether to try a ReStart on the hanging TaskProcessor
        /// </summary>
        protected virtual bool UseRestart { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether the Worker of the hanging TaskProcessor must be reset as well
        /// </summary>
        protected virtual bool ResetWorker { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether the hanging TaskProcessor shoud be completly killed and removed from available Workers
        /// </summary>
        protected virtual bool UseKill { get; set; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether the killed TaskProcessor should be replaced with a new instance
        /// </summary>
        protected virtual bool ReCreateOnKill { get; set; } = true;

        /// <summary>
        /// Checks whether the given taskProcessor is alive and takes appropriate actions if the processor is corrupted
        /// </summary>
        /// <param name="processor">the processor that is being watched</param>
        protected override void WatchProcessorInstance(ITaskProcessor processor)
        {
            if (DateTime.Now.Subtract(processor.LastActivity).TotalMilliseconds > timeout)
            {
                bool statusFixed = false;
                var currentTask = processor.Worker.CurrentTask;
                if (UseRestart)
                {
                    try
                    {
                        RestartProcessor(processor, ResetWorker);
                        statusFixed = true;
                    }
                    catch (Exception ex)
                    {
                        LogEnvironment.LogEvent($"Restart of processor has failed: {ex.OutlineException()}", LogSeverity.Error);
                        processor.Zombie();
                    }
                }

                if (!statusFixed && UseKill)
                {
                    try
                    {
                        KillProcessor(processor, ReCreateOnKill);
                        statusFixed = true;
                    }
                    catch (Exception ex)
                    {
                        LogEnvironment.LogEvent($"Kill of processor has failed: {ex.OutlineException()}", LogSeverity.Error);
                        processor.Zombie();
                    }
                }

                HandleFailedTask(currentTask);
                if (!statusFixed)
                {
                    LogEnvironment.LogDebugEvent("Processorstatus could not be fixed. Requesting application-shutdown...", LogSeverity.Error);
                    Critical();
                }
            }
        }

        /// <summary>
        /// Enables a derived class to perform checks on the Task that caused the freeze of a worker
        /// </summary>
        /// <param name="task">the Task that presumably caused a worker to freeze.</param>
        protected virtual void HandleFailedTask(ITask task)
        {
            task.Fail(new TimeoutException("The Execution of this task was aborted, after a Timeout."));
        }
    }
}
