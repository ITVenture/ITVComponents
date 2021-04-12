using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.Logging;
using ITVComponents.Plugins;
using ITVComponents.Serialization;
using ITVComponents.Threading;

namespace ITVComponents.ParallelProcessing
{
    /// <summary>
    /// The Abstract core definition of a Parallel task processing unit
    /// </summary>
    public abstract class ParallelTaskProcessor: IStatusSerializable, IStoppable
    {
        /// <summary>
        /// Holds a list of all available processors that were initialized in the current process
        /// </summary>
        private static ConcurrentDictionary<string, ParallelTaskProcessor> initializedProcessors;

        /// <summary>
        /// the refresh timeout of the moderator timer object 
        /// </summary>
        private int workerPollTime;

        /// <summary>
        /// the minimum taskCount in the queue before new Tasks are being acquired
        /// </summary>
        private int lowTaskThreshold;

        /// <summary>
        /// the maximum taskCount in the queue before enqueueing is suspended
        /// </summary>
        private int highTaskThreshold;

        private readonly bool useAffineThreads;

        /// <summary>
        /// indicates whether the GetMoreJobs event needs to be triggered
        /// </summary>
        private Dictionary<int, bool> filling;

        /// <summary>
        /// All Tasks of this Parallel TaskProcessingUnit
        /// </summary>
        private Dictionary<int, ConcurrentQueue<TaskContainer>> tasks;

        /// <summary>
        /// Pulse object used to trigger workers to poll the queues
        /// </summary>
        private object workerPulse;

        /// <summary>
        /// Ends all workers when set
        /// </summary>
        private ManualResetEvent stopEvent;

        /// <summary>
        /// 
        /// </summary>
        private ManualResetEvent stoppedEvent;

        /// <summary>
        /// Processors that are working for this ParallelTaskProcessor instance
        /// </summary>
        private List<TaskProcessor> processors;

        /// <summary>
        /// ThreadTimer used to moderate the queues 
        /// </summary>
        private Timer moderatorTimer;

        /// <summary>
        /// indicates whether this object has been disposed
        /// </summary>
        private bool disposed;

        /// <summary>
        /// the unique identifier of this ParallelTaskProcessor instance
        /// </summary>
        private string identifier;

        /// <summary>
        /// A Callback that can be used to create new worker instances
        /// </summary>
        private readonly Func<ITaskWorker> worker;

        /// <summary>
        /// The highest priority for Jobs
        /// </summary>
        private readonly int highestPriority;

        /// <summary>
        /// the lowes priority for tasks
        /// </summary>
        private readonly int lowestPriority;

        /// <summary>
        /// the number of workers to have on this parallelTaskProcessor instance
        /// </summary>
        private readonly int workerCount;

        /// <summary>
        /// Watches over the workers and takes appropriate actions if a worker changes into a non-responsive status
        /// </summary>
        private readonly WatchDog watchDog;

        /// <summary>
        /// initializes static members of the ParallelTaskProcessor class
        /// </summary>
        static ParallelTaskProcessor()
        {
            initializedProcessors = new ConcurrentDictionary<string, ParallelTaskProcessor>();
        }

        /// <summary>
        /// Initializes a new instance of the TaskWorker class
        /// </summary>
        /// <param name="identifier">the unique identifier of this taskprocessor</param>
        /// <param name="worker">the worker that is used by all ThreadWorkers</param>
        /// <param name="highestPriority">the highest Priority that is supported by this parallelProcessor</param>
        /// <param name="lowestPriority">the lowest Priority that is supported by this parallelProcessor</param>
        /// <param name="workerCount">the number of workers that are initialized in this ParallelTaskProcessor</param>
        /// <param name="workerPollTime">the number of milliseconds a worker waits between single cycles with nothing to do</param>
        /// <param name="lowTaskThreshold">the minimum number of Items that should be in a queue for permanent processing</param>
        /// <param name="highTaskThreshold">the maximum number of Items that should be queued in a workerQueue</param>
        /// <param name="useAffineThreads">indicates whether to use ThreadAffinity in the workers</param>
        /// <param name="watchDog">a watchdog instance that will restart worker-instances when they become unresponsive</param>
        protected ParallelTaskProcessor(string identifier, Func<ITaskWorker> worker, int highestPriority, int lowestPriority, int workerCount, int workerPollTime, int lowTaskThreshold, int highTaskThreshold, bool useAffineThreads, WatchDog watchDog) :
            this(identifier, worker, highestPriority, lowestPriority, workerCount, workerPollTime, lowTaskThreshold, highTaskThreshold, useAffineThreads)
        {
            this.watchDog = watchDog;
        }

        /// <summary>
        /// Initializes a new instance of the TaskWorker class
        /// </summary>
        /// <param name="identifier">the unique identifier of this taskprocessor</param>
        /// <param name="worker">the worker that is used by all ThreadWorkers</param>
        /// <param name="highestPriority">the highest Priority that is supported by this parallelProcessor</param>
        /// <param name="lowestPriority">the lowest Priority that is supported by this parallelProcessor</param>
        /// <param name="workerCount">the number of workers that are initialized in this ParallelTaskProcessor</param>
        /// <param name="workerPollTime">the number of milliseconds a worker waits between single cycles with nothing to do</param>
        /// <param name="lowTaskThreshold">the minimum number of Items that should be in a queue for permanent processing</param>
        /// <param name="highTaskThreshold">the maximum number of Items that should be queued in a workerQueue</param>
        /// <param name="useAffineThreads">indicates whether to use ThreadAffinity in the workers</param>
        protected ParallelTaskProcessor(string identifier, Func<ITaskWorker> worker, int highestPriority, int lowestPriority, int workerCount, int workerPollTime, int lowTaskThreshold, int highTaskThreshold, bool useAffineThreads)
            : this()
        {
            this.identifier = identifier;
            this.worker = worker;
            this.highestPriority = highestPriority;
            this.lowestPriority = lowestPriority;
            this.workerCount = workerCount;
            initializedProcessors.TryAdd(identifier, this);
            this.lowTaskThreshold = lowTaskThreshold;
            this.highTaskThreshold = highTaskThreshold;
            this.useAffineThreads = useAffineThreads;
            this.workerPollTime = workerPollTime;
            if (lowestPriority < highestPriority)
            {
                throw new ArgumentException("Higher Priorities must have lower numbers (e.g. low=5, high=1)");
            }

            for (int i = highestPriority; i <= lowestPriority; i++)
            {
                tasks.Add(i, new ConcurrentQueue<TaskContainer>());
                filling.Add(i, true);
            }

            int exclusiveCount = 1;
            bool roundClock = false;
            int lo = lowestPriority;
            for (int i = 0; i < workerCount; i++)
            {
                CreateProcessor(lo);
                exclusiveCount--;
                if (exclusiveCount == 0)
                {
                    if (lo == highestPriority)
                    {
                        roundClock = true;
                    }

                    lo = lo > highestPriority ? lo-1 : lo;
                    exclusiveCount = (lowestPriority - lo) + 1;
                }
            }

            if (lo != highestPriority && !roundClock)
            {
                CreateProcessor(highestPriority);
            }
        }

        /// <summary>
        /// Prevents a default instance of the ParallelTaskProcessor class from being created
        /// </summary>
        private ParallelTaskProcessor()
        {
            tasks = new Dictionary<int, ConcurrentQueue<TaskContainer>>();
            filling = new Dictionary<int, bool>();
            workerPulse = new object();
            stopEvent = new ManualResetEvent(false);
            stoppedEvent = new ManualResetEvent(false);
            processors = new List<TaskProcessor>();
            moderatorTimer = new Timer(Moderate,string.Format("::{0}::", GetHashCode()), Timeout.Infinite,
                                         Timeout.Infinite);
        }

        /// <summary>
        /// Gets the unique identifier of this TaskQueue
        /// </summary>
        internal string Identifier
        {
            get { return identifier; }
        }

        /// <summary>
        /// Gets an instance of a ParallelTaskProcessor with the given unique identifier
        /// </summary>
        /// <param name="identifier">the name of the desired processor</param>
        /// <returns></returns>
        internal static ParallelTaskProcessor GetInstance(string identifier)
        {
            ParallelTaskProcessor retVal;
            initializedProcessors.TryGetValue(identifier, out retVal);
            return retVal;
        }

        /// <summary>
        /// Suspends all activity on this ParallelTaskProcessor instance
        /// </summary>
        public void Suspend()
        {
            moderatorTimer.Change(Timeout.Infinite, Timeout.Infinite);
            TaskProcessor[] procs;
            lock (processors)
            {
                procs = processors.ToArray();
            }
            foreach (var processor in procs)
            {
                processor.Suspend();
            }
        }

        /// <summary>
        /// Resumes the activity on this ParallelTaskProcessor instance
        /// </summary>
        public void Resume()
        {
            TaskProcessor[] procs;
            lock (processors)
            {
                procs = processors.ToArray();
            }
            foreach (var processor in procs)
            {
                processor.Resume();
            }

            moderatorTimer.Change(workerPollTime, workerPollTime);
        }

        /// <summary>
        /// Gets the Runtime information required to restore the status when the application restarts
        /// </summary>
        /// <returns>an object serializer containing all required data for object re-construction on application reboot</returns>
        public virtual RuntimeInformation GetPostDisposalSerializableStaus()
        {
            RuntimeInformation retVal = new RuntimeInformation();
            foreach (KeyValuePair<int, ConcurrentQueue<TaskContainer>> list in tasks)
            {
                retVal.Add(string.Format("TaskList_{0}", list.Key), list.Value.ToArray());
            }

            return retVal;
        }

        /// <summary>
        /// Applies Runtime information that was loaded from a file
        /// </summary>
        /// <param name="runtimeInformation">the runtime information describing the status of this object before the last shutdown</param>
        public virtual void LoadRuntimeStatus(RuntimeInformation runtimeInformation)
        {
            foreach (KeyValuePair<int, ConcurrentQueue<TaskContainer>> list in tasks)
            {
                TaskContainer[] taskArray = (TaskContainer[])runtimeInformation[string.Format("TaskList_{0}", list.Key)];
                foreach (TaskContainer task in taskArray)
                {
                    OnIntegratePendingTask(task.Task);
                    list.Value.Enqueue(task);
                }
            }

            lock (workerPulse)
            {
                Monitor.Pulse(workerPulse);
            }
        }

        /// <summary>
        /// Allows this object to do required initializations when no runtime status is provided by the calling object
        /// </summary>
        public virtual void InitializeWithoutRuntimeInformation()
        {
        }

        /// <summary>
        /// Is called when the runtime is completly available and ready to run
        /// </summary>
        public void RuntimeReady()
        {
            moderatorTimer.Change(workerPollTime, workerPollTime);
        }

        /// <summary>
        /// Führt anwendungsspezifische Aufgaben aus, die mit dem Freigeben, Zurückgeben oder Zurücksetzen von nicht verwalteten Ressourcen zusammenhängen.
        /// </summary>
        /// <filterpriority>2</filterpriority>
        public void Dispose()
        {
            if (!disposed)
            {
                ParallelTaskProcessor t;
                initializedProcessors.TryRemove(identifier,out t);
                disposed = true;
                TaskProcessor[] procs;
                lock (processors)
                {
                    procs = processors.ToArray();
                }
                foreach (TaskProcessor processor in procs)
                {
                    ((IDisposable)processor).Dispose();
                }

                moderatorTimer.Dispose();
                stopEvent.Dispose();
                stoppedEvent.Dispose();
            }
        }

        /// <summary>
        /// Stops Processes that are running inside the current Plugin
        /// </summary>
        public void Stop()
        {
            stopEvent.Set();
            TaskProcessor[] procs;
            lock (processors)
            {
                procs = processors.ToArray();
            }
            foreach (TaskProcessor processor in procs)
            {
                processor.Join();
            }

            stoppedEvent.WaitOne();
            moderatorTimer.Change(Timeout.Infinite, Timeout.Infinite);
        }

        /// <summary>
        /// Adds a task that was scheduled for execution
        /// </summary>
        /// <param name="task">the task that was been selected for execution</param>
        internal void TaskScheduled(TaskContainer task)
        {
            ConcurrentQueue<TaskContainer> queue = tasks[task.Task.Priority];
            if (task.Task.Active)
            {
                queue.Enqueue(task);
                lock (workerPulse)
                {
                    Monitor.Pulse(workerPulse);
                }
            }
            else
            {
                task.Task.Dispose();
            }
        }

        /// <summary>
        /// Removes a processor from the list of active processors
        /// </summary>
        /// <param name="processor">a processor that is being stopped</param>
        internal void UnRegisterProcessor(TaskProcessor processor)
        {
            lock (processors)
            {
                processors.Remove(processor);
                ((IDisposable)processor).Dispose();
            }
        }

        /// <summary>
        /// Creates a new taskProcessor instance
        /// </summary>
        /// <param name="startPriority">the minimum-priority for this processor to process</param>
        internal void CreateProcessor(int startPriority)
        {
            lock (processors)
            {
                processors.Add(new TaskProcessor(worker(), workerPulse, stopEvent, workerPollTime, highestPriority, startPriority, this, tasks, useAffineThreads));
            }
        }

        /// <summary>
        /// Enqueues a task into the appropriate queue
        /// </summary>
        /// <param name="task">the task that needs to be queued</param>
        protected bool EnqueueTask(ITask task)
        {
            try
            {
                bool scheduled = false;
                Dictionary<string, TaskScheduler.ScheduleRequest> requests =
                new Dictionary<string, TaskScheduler.ScheduleRequest>();
                using (task.DemandExclusive())
                {
                    foreach (SchedulerPolicy policy in task.Schedules)
                    {
                        if (string.IsNullOrEmpty(policy.SchedulerName) || !TaskScheduler.SchedulerExists(policy.SchedulerName))
                        {
                            if (!string.IsNullOrEmpty(policy.SchedulerName))
                            {
                                LogEnvironment.LogDebugEvent(string.Format("Unable to find Scheduler @ParallelTaskProcessor Line 311: {0}", policy.SchedulerName), LogSeverity.Warning);
                            }
                        }
                        else
                        {
                            TaskScheduler scheduler = TaskScheduler.GetScheduler(policy.SchedulerName);
                            var policyContext = (scheduler as ISchedulerlPolicyContextProvider)?.EnterPolicyContext();
                            try
                            {
                                if (requests.ContainsKey(policy.SchedulerName))
                                {
                                    requests[policy.SchedulerName].AddInstruction(policy.SchedulerInstruction);
                                }
                                else
                                {
                                    var request = scheduler.CreateRequest(this, task);
                                    requests.Add(policy.SchedulerName,request);
                                    request.AddInstruction(policy.SchedulerInstruction);
                                }
                            }
                            finally
                            {
                                policyContext?.Dispose();
                            }
                        }
                    }
                }

                foreach (KeyValuePair<string, TaskScheduler.ScheduleRequest> req in requests)
                {
                    TaskScheduler scheduler = TaskScheduler.GetScheduler(req.Key);
                    scheduled |= scheduler.ScheduleTask(req.Value);
                }

                if (!scheduled)
                {
                    TaskScheduled(new TaskContainer {Task= task});
                }

                return true;
            }
            catch (Exception ex)
            {
                /*bool recoverable = true;
                if (ex is ComponentException cex)
                {
                    recoverable = !cex.Critical;
                }*/

                task.Fail(ex);
                LogEnvironment.LogDebugEvent(string.Format("Failed to Enqueue a Task @ParallelTaskProcessor Line 345: {0}", ex), LogSeverity.Warning);
            }

            return false;
        }

        /// <summary>
        /// Raises the GetMoreTasks event in order to inform a client object that a priority queue may soon be running out of work
        /// </summary>
        /// <param name="priority">the priority for which to get new tasks to do</param>
        protected virtual void OnGetMoreTasks(int priority)
        {
            if (GetMoreTasks != null)
            {
                GetMoreTasks(this, new GetMoreTasksEventArgs {Priority = priority});
            }
        }

        /// <summary>
        /// Raises the IntegratePendingTask - Event in order to prepare pending objects for being processed by a worker
        /// </summary>
        /// <param name="task">the Task that has been loaded from the runtime status</param>
        protected virtual void OnIntegratePendingTask(ITask task)
        {
            if (IntegratePendingTask != null)
            {
                IntegratePendingTask(this, new IntegrationEventArgs(task));
            }
        }

        /// <summary>
        /// Integrates a Task back into the runtime environment
        /// </summary>
        /// <param name="task">the task that was deserialized after restarting the application</param>
        internal void IntegrateTask(ITask task)
        {
            OnIntegratePendingTask(task);
        }

        /// <summary>
        /// Checks whether more work is required for the worker queues
        /// </summary>
        /// <param name="state">the unused object state</param>
        private void Moderate(object state)
        {
            bool reactivate = false;
            state.LocalOwner(state.ToString());
            moderatorTimer.Change(Timeout.Infinite, Timeout.Infinite);
            try
            {
                if (!stopEvent.SafeWaitHandle.IsClosed && !stopEvent.WaitOne(10))
                {
                    reactivate = true;
                    foreach (KeyValuePair<int, ConcurrentQueue<TaskContainer>> list in tasks)
                    {
                        if (filling[list.Key] && list.Value.Count > highTaskThreshold)
                        {
                            filling[list.Key] = false;
                        }
                        else if (!filling[list.Key] && list.Value.Count < lowTaskThreshold)
                        {
                            filling[list.Key] = true;
                        }

                        if (filling[list.Key])
                        {
                            OnGetMoreTasks(list.Key);
                        }
                    }

                    if (watchDog != null)
                    {
                        TaskProcessor[] procs;
                        lock (processors)
                        {
                            procs = processors.ToArray();
                        }

                        foreach (var proc in procs)
                        {
                            watchDog.WatchProcessor(proc);
                        }
                    }
                }
                else
                {
                    stoppedEvent.Set();
                }
            }
            finally
            {
                if (reactivate)
                {
                    moderatorTimer.Change(workerPollTime, workerPollTime);
                }

                state.LocalOwner(null);
            }
        }

        /// <summary>
        /// Triggers a client object to provide more tasks that need to be executed.
        /// </summary>
        public event EventHandler<GetMoreTasksEventArgs> GetMoreTasks;

        /// <summary>
        /// Triggers a client object to perform required actions in order to integrate a deserialized task into the current runtime environment
        /// </summary>
        public event EventHandler<IntegrationEventArgs> IntegratePendingTask;
    }
}
