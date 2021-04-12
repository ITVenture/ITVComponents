using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.Logging;

namespace ITVComponents.ParallelProcessing
{
    public sealed class AsyncTaskProcessor:IDisposable, ITaskProcessor
    {
        /// <summary>
        /// the exponent used to get an accurate task priority scheduling
        /// </summary>
        private const int PriorityExponent = 3;

        /// <summary>
        /// The worker that is used to process a task
        /// </summary>
        private ITaskWorker worker;

        /// <summary>
        /// a worker pulse object that is used to get triggered updates on further tasks
        /// </summary>
        private object workerPulse;

        /// <summary>
        /// ResetEvent that is used to stop the processing thread
        /// </summary>
        private System.Threading.ManualResetEvent stopEvent;

        /// <summary>
        /// the worker PollTime
        /// </summary>
        private int workerPollTime;

        /// <summary>
        /// the highest priority that is processed by this worker
        /// </summary>
        private int highestPriority;

        /// <summary>
        /// the lowest priority that is processed by this worker
        /// </summary>
        private int lowestPriority;

        /// <summary>
        /// the task queues for this processor
        /// </summary>
        private Dictionary<int, System.Collections.Concurrent.ConcurrentQueue<TaskContainer>> tasks;

        /// <summary>
        /// Identifies the processing row for a single processing cycle
        /// </summary>
        private List<int> processCycle;

        /// <summary>
        /// a Task object that represents an endlessly running thread that processes the queues
        /// </summary>
        private Task workerTask;

        /// <summary>
        /// Waits until the Taskprocessing thread has been activated
        /// </summary>
        private ManualResetEvent startupWait;

        /// <summary>
        /// Waits before entering the processing loop
        /// </summary>
        private ManualResetEvent enterprocessingLoopWait;

        /// <summary>
        /// is Set when the taskprocessor is allowed to continue working and reset when it must pause
        /// </summary>
        private ManualResetEvent suspendEvent;

        /// <summary>
        /// is set to signaled before the thread waits for the suspendEvent to be set
        /// </summary>
        private ManualResetEvent suspendedEvent;

        /// <summary>
        /// the parent from which this taskProcessor gets Tasks to process
        /// </summary>
        private ParallelTaskProcessor parent;

        /// <summary>
        /// holds the last activity of this processor
        /// </summary>
        private DateTime lastActivity;

        /// <summary>
        /// /The current state of this processor instance
        /// </summary>
        private TaskProcessorState currentState = TaskProcessorState.Idle;

        /// <summary>
        /// Initializes a new instance of the TaskProcessor class
        /// </summary>
        /// <param name="worker">the worker that is used to process tasks</param>
        /// <param name="workerPulse">an object that is used to pulse this worker when new tasks have arrived</param>
        /// <param name="stopEvent">a reset event used to stop this processor</param>
        /// <param name="workerPollTime">the pollTime after which a new poll cycle is started when no further intervetion is done</param>
        /// <param name="highestPriority">the highest priority that is processed by this worker</param>
        /// <param name="lowestPriority">the lowest priority that is processed by this worker</param>
        /// <param name="parent">the parent from which this processor gets tasks to process</param>
        /// <param name="tasks">the task queues that are used to process tasks</param>
        internal AsyncTaskProcessor(ITaskWorker worker, object workerPulse, System.Threading.ManualResetEvent stopEvent,
                               int workerPollTime, int highestPriority, int lowestPriority,
                                ParallelTaskProcessor parent,
                               Dictionary<int, System.Collections.Concurrent.ConcurrentQueue<TaskContainer>> tasks)
            : this()
        {
            this.worker = worker;
            this.workerPulse = workerPulse;
            this.stopEvent = stopEvent;
            this.workerPollTime = workerPollTime;
            this.highestPriority = highestPriority;
            this.lowestPriority = lowestPriority;
            this.tasks = tasks;
            this.parent = parent;
            ((ITaskProcessor) this).StartupThread();
            BuildProcessCycle();
            lastActivity = DateTime.Now;
            enterprocessingLoopWait.Set();
        }

        /// <summary>
        /// Prevents a default instance of the TaskProcessor class from being created
        /// </summary>
        private AsyncTaskProcessor()
        {
            suspendEvent = new ManualResetEvent(true);
            suspendedEvent = new ManualResetEvent(false);
            processCycle = new List<int>();
            startupWait = new ManualResetEvent(false);
            enterprocessingLoopWait = new ManualResetEvent(false);
        }

        /// <summary>
        /// Gets the Worker-instance that is used by this Processor instance
        /// </summary>
        ITaskWorker ITaskProcessor.Worker => worker;

        /// <summary>
        /// Gets the last time the workerthread was in a defined-status
        /// </summary>
        public DateTime LastActivity => lastActivity;

        /// <summary>
        /// Gets the current running-state of this TaskProcessor instance
        /// </summary>
        TaskProcessorState ITaskProcessor.State => currentState;

        /// <summary>
        /// gets the parent queue of this processor instance
        /// </summary>
        ParallelTaskProcessor ITaskProcessor.Parent => parent;

        /// <summary>
        /// Gets the lowest priority that is processed with this TaskProcessor instance
        /// </summary>
        int ITaskProcessor.LowestPriority => lowestPriority;

        /// <summary>
        /// Waits until this worker has stopped
        /// </summary>
        void ITaskProcessor.Join()
        {
            currentState = TaskProcessorState.Stopping;
            workerTask.GetAwaiter().GetResult();
            currentState = TaskProcessorState.Stopped;
        }

        /// <summary>
        /// Führt anwendungsspezifische Aufgaben durch, die mit der Freigabe, der Zurückgabe oder dem Zurücksetzen von nicht verwalteten Ressourcen zusammenhängen.
        /// </summary>
        /// <filterpriority>2</filterpriority>
        void IDisposable.Dispose()
        {
            startupWait.Dispose();
            enterprocessingLoopWait.Dispose();
        }

        /// <summary>
        /// Sets this worker into a wait-state
        /// </summary>
        void ITaskProcessor.Suspend()
        {
            currentState = TaskProcessorState.Stopping;
            suspendEvent.Reset();
            suspendedEvent.WaitOne();
            currentState = TaskProcessorState.Stopped;
        }

        /// <summary>
        /// Resumes this worker
        /// </summary>
        void ITaskProcessor.Resume()
        {
            lastActivity = DateTime.Now;
            suspendedEvent.Reset();
            suspendEvent.Set();
            currentState = TaskProcessorState.Running;
        }

        /// <summary>
        /// Kills the thread that is powering this processor
        /// </summary>
        void ITaskProcessor.KillThread()
        {
            suspendEvent.Set();
            suspendedEvent.Reset();
            startupWait.Reset();
            currentState = TaskProcessorState.Idle;
            //enterprocessingLoopWait.Reset();
        }

        /// <summary>
        /// Sets this processor into a zombie mode where it won't process any tasks
        /// </summary>
        void ITaskProcessor.Zombie()
        {
            LogEnvironment.LogDebugEvent("This TaskProcessor just turned into a zombie!", LogSeverity.Warning);
            currentState = TaskProcessorState.Zombie;
        }

        /// <summary>
        /// Starts the thread
        /// </summary>
        void ITaskProcessor.StartupThread()
        {
            Task.Run(Work);
            startupWait.WaitOne();
            currentState = TaskProcessorState.Running;
        }

        /// <summary>
        /// Builds the Process cycle used for this TaskProcessor instance
        /// </summary>
        private void BuildProcessCycle()
        {
            Random rnd = new Random();
            List<KeyValuePair<int, int>> l = new List<KeyValuePair<int, int>>();
            int priorityCount = (lowestPriority - highestPriority) + 1;
            for (int i = highestPriority, a=0; i <= lowestPriority; i++, a++)
            {
                int mx = priorityCount - a;
                mx = (int)Math.Pow(mx, PriorityExponent);
                l.Add(new KeyValuePair<int, int>(i, mx));
            }

            while (l.Count != 0)
            {
                int i;
                if (l.Count > 1)
                {
                    i = rnd.Next(0, l.Count - 1);
                }
                else
                {
                    i = 0;
                }

                processCycle.Add(l[i].Key);
                KeyValuePair<int, int> tmp = new KeyValuePair<int, int>(l[i].Key, l[i].Value - 1);
                if (tmp.Value > 0)
                {
                    l[i] = tmp;
                }
                else
                {
                    l.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// Works the all working queues
        /// </summary>
        private async Task Work()
        {
            startupWait.Set();
            enterprocessingLoopWait.WaitOne();
            while (true)
            {
                lastActivity = DateTime.Now;
                bool tasksFound = false;
                bool ending = false;
                if (!suspendEvent.WaitOne(10000))
                {
                    suspendedEvent.Set();
                    while (!suspendEvent.WaitOne(1000))
                    {
                        if (stopEvent.WaitOne(50))
                        {
                            ending = true;
                            break;
                        }
                    }
                }

                if (ending)
                {
                    break;
                }

                if (currentState == TaskProcessorState.Running)
                {
                    foreach (int i in processCycle)
                    {
                        TaskContainer task;
                        lastActivity = DateTime.Now;
                        if ((!(ending = stopEvent.WaitOne(50))) && tasks[i].TryDequeue(out task))
                        {
                            try
                            {
                                await CheckScheduleAsync(task, async t =>
                                {
                                    try
                                    {
                                        tasksFound = true;
                                        await worker.ProcessAsync(t);
                                        t.Fulfill();
                                    }
                                    catch (Exception ex)
                                    {
                                        LogEnvironment.LogDebugEvent(ex.ToString(), LogSeverity.Error);
                                        /*bool recoverable = true;
                                        if (ex is ComponentException cex)
                                        {
                                            recoverable = !cex.Critical;
                                        }*/

                                        task.Task.Fail(ex);
                                    }
                                });
                            }
                            catch (Exception ex)
                            {
                                LogEnvironment.LogDebugEvent(ex.ToString(), LogSeverity.Error);
                                /*bool recoverable = true;
                                if (ex is ComponentException cex)
                                {
                                    recoverable = !cex.Critical;
                                }*/

                                task.Task.Fail(ex);
                            }
                        }
                        else if (ending)
                        {
                            break;
                        }

                        lastActivity = DateTime.Now;
                    }
                }
                else
                {
                    LogEnvironment.LogDebugEvent($"CurrentState is {currentState}. Not doing anything...", LogSeverity.Warning);
                    ending = stopEvent.WaitOne(50);
                }

                if (!tasksFound && !ending)
                {
                    worker.Idle();
                    lock (workerPulse)
                    {
                        Monitor.Wait(workerPulse, workerPollTime);
                    }
                }
                else if (ending)
                {
                    break;
                }
            }
        }

        /// <summary>
        /// Checks the schedule for a specific task
        /// </summary>
        /// <param name="task">the task for which to create a schedule request</param>
        /// <param name="action">the action to take when the schedule applies</param>
        private void CheckSchedule(TaskContainer task, Action<ITask> action)
        {
            bool scheduled = false;
            if (task.Request?.SchedulerName != null)
            {
                TaskScheduler scl = TaskScheduler.GetScheduler(task.Request.SchedulerName);
                if (scl.RunScheduledTask(task.Request, action))
                {
                    scheduled = true;
                }
            }

            if (!scheduled && task.Task.Active)
            {
                action(task.Task);
            }
        }

        /// <summary>
        /// Checks the schedule for a specific task
        /// </summary>
        /// <param name="task">the task for which to create a schedule request</param>
        /// <param name="action">the action to take when the schedule applies</param>
        private async Task CheckScheduleAsync (TaskContainer task, Func<ITask,Task> action)
        {
            bool scheduled = false;
            if (task.Request?.SchedulerName != null)
            {
                TaskScheduler scl = TaskScheduler.GetScheduler(task.Request.SchedulerName);
                if (await scl.RunScheduledTaskAsync(task.Request, action))
                {
                    scheduled = true;
                }
            }

            if (!scheduled && task.Task.Active)
            {
                await action(task.Task);
            }
        }
    }
}