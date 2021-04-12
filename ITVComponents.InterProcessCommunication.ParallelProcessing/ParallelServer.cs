using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Authentication;
using System.Security.Principal;
using System.Text;
using System.Threading;
using ITVComponents.InterProcessCommunication.ParallelProcessing.Resources;
using ITVComponents.InterProcessCommunication.Shared.Security;
using ITVComponents.Logging;
using ITVComponents.ParallelProcessing;
using ITVComponents.Plugins;
using ITVComponents.Plugins.Management;
using ITVComponents.Serialization;
using ITVComponents.Threading;

namespace ITVComponents.InterProcessCommunication.ParallelProcessing
{
    public abstract class ParallelServer<TPackage, TTask>: IParallelServer, IStatusSerializable, IStatisticsProvider, IStoppable, IDeferredInit
                                                                      where TTask:IProcessTask 
                                                                      where TPackage:IProcessPackage 
    {
        /// <summary>
        /// TaskProcessor class that is used to process the provided items coming from the client
        /// </summary>
        private ParallelTaskProcessor<TTask> processor;

        /// <summary>
        /// Holds a list of packages that require processing 
        /// </summary>
        private Dictionary<int, ConcurrentBag<TPackage>> packages;

        /// <summary>
        /// Holds a list of packages that are currently in progress
        /// </summary>
        private List<TPackage> workingPackages;

        /// <summary>
        /// Holds a list of events that have been triggered and possibly need to be re-triggered
        /// </summary>
        private List<PackageFinishedEventArgsReTriggerContainer> triggeredEvents;

        /// <summary>
        /// the timeout after which an event that has not been commited by a client is being re-triggered
        /// </summary>
        private int eventReTriggerInterval;

        /// <summary>
        /// Timer instance that re-triggers event that were not commited in a timely fashion
        /// </summary>
        private Timer eventReTriggerer;

        /// <summary>
        /// the highest priority that is processed by this service
        /// </summary>
        private int highestPriority;

        /// <summary>
        /// the lowest priority that is processed by this service
        /// </summary>
        private int lowestPriority;

        /// <summary>
        /// the number of workers to use
        /// </summary>
        private int workerCount;

        /// <summary>
        /// the interval after which a worker will poll the working queue if it has not been triggered
        /// </summary>
        private int workerPollInterval;

        /// <summary>
        /// the minimum number of tasks that the workerqueue should contain
        /// </summary>
        private int lowTaskThreshold;

        /// <summary>
        /// the maximum number of tasks that the workerqueue should contain
        /// </summary>
        private int highTaskThreshold;

        /// <summary>
        /// indicates whether to use affine threads
        /// </summary>
        private bool useAffineThreads;

        /// <summary>
        /// indicates whether to collect statistic data
        /// </summary>
        private bool collectStats = false;

        /// <summary>
        /// Collector object for collecting statistics information about the parallel processing in this item
        /// </summary>
        private StatisticData statData;

        /// <summary>
        /// a value indicating how many per-item fails are allowed on this server
        /// </summary>
        private int maximumFailsPerItem = 0;

        /// <summary>
        /// the plugin factory that is used for initializing forther plugins
        /// </summary>
        private PluginFactory factory;

        /// <summary>
        /// a watchdog instance that is used to restart non-responsive processors
        /// </summary>
        private WatchDog watchDog;

        /// <summary>
        /// Initializes a new instance of the ParallelServer class
        /// </summary>
        /// <param name="highestPriority">the highest priority that is processed by this server</param>
        /// <param name="lowestPriority">the lowest priority that is processed by this server</param>
        /// <param name="workerCount">the number of workers to use</param>
        /// <param name="workerPollInterval">the interval after which a worker will poll the working queue if it has not been triggered</param>
        /// <param name="lowTaskThreshold">the minimum number of tasks that the workerqueue should contain</param>
        /// <param name="highTaskThreshold">the maximum number of tasks that the workerqueue should contain</param>
        /// <param name="useAffineThreads">indicates whether to use affine threads</param>
        /// <param name="eventReTriggerInterval">the timeout in minutes after which an event that has not been commited by a client is re-triggered</param>
        /// <param name="factory">the factory that is used to initialize further plugins</param>
        protected ParallelServer(int highestPriority, int lowestPriority, int workerCount, int workerPollInterval, int lowTaskThreshold,
                                 int highTaskThreshold, bool useAffineThreads, int eventReTriggerInterval, PluginFactory factory)
            : this(
                highestPriority, lowestPriority, workerCount, workerPollInterval, lowTaskThreshold, highTaskThreshold, useAffineThreads,
                eventReTriggerInterval, 0, factory)
        {
        }

        /// <summary>
        /// Initializes a new instance of the ParallelServer class
        /// </summary>
        /// <param name="highestPriority">the highest priority that is processed by this server</param>
        /// <param name="lowestPriority">the lowest priority that is processed by this server</param>
        /// <param name="workerCount">the number of workers to use</param>
        /// <param name="workerPollInterval">the interval after which a worker will poll the working queue if it has not been triggered</param>
        /// <param name="lowTaskThreshold">the minimum number of tasks that the workerqueue should contain</param>
        /// <param name="highTaskThreshold">the maximum number of tasks that the workerqueue should contain</param>
        /// <param name="useAffineThreads">indicates whether to use affine threads</param>
        /// <param name="eventReTriggerInterval">the timeout in minutes after which an event that has not been commited by a client is re-triggered</param>
        /// <param name="factory">the factory that is used to initialize further plugins</param>
        /// <param name="watchDog">a watchdog instance that is used to restart non-responsive processors</param>
        protected ParallelServer(int highestPriority, int lowestPriority, int workerCount, int workerPollInterval, int lowTaskThreshold,
            int highTaskThreshold, bool useAffineThreads, int eventReTriggerInterval, PluginFactory factory, WatchDog watchDog)
            : this(
                highestPriority, lowestPriority, workerCount, workerPollInterval, lowTaskThreshold, highTaskThreshold, useAffineThreads,
                eventReTriggerInterval, 0, factory, watchDog)
        {
        }

        /// <summary>
        /// Initializes a new instance of the ParallelServer class
        /// </summary>
        /// <param name="highestPriority">the highest priority that is processed by this server</param>
        /// <param name="lowestPriority">the lowest priority that is processed by this server</param>
        /// <param name="workerCount">the number of workers to use</param>
        /// <param name="workerPollInterval">the interval after which a worker will poll the working queue if it has not been triggered</param>
        /// <param name="lowTaskThreshold">the minimum number of tasks that the workerqueue should contain</param>
        /// <param name="highTaskThreshold">the maximum number of tasks that the workerqueue should contain</param>
        /// <param name="useAffineThreads">indicates whether to use affine threads</param>
        /// <param name="eventReTriggerInterval">the timeout in minutes after which an event that has not been commited by a client is re-triggered</param>
        /// <param name="maximumFailsPerItem">defines how many times a task can fail before it is considered a failure</param>
        /// <param name="factory">the factory that is used to initialize further plugins</param>
        /// <param name="watchDog">a watchdog instance that is used to restart non-responsive processors</param>
        protected ParallelServer(int highestPriority, int lowestPriority, int workerCount, int workerPollInterval, int lowTaskThreshold, int highTaskThreshold, bool useAffineThreads, int eventReTriggerInterval, int maximumFailsPerItem, PluginFactory factory, WatchDog watchDog) :
            this(highestPriority, lowestPriority, workerCount, workerPollInterval, lowTaskThreshold, highTaskThreshold, useAffineThreads, eventReTriggerInterval, maximumFailsPerItem, factory)
        {
            this.watchDog = watchDog;
        }

        /// <summary>
        /// Initializes a new instance of the ParallelServer class
        /// </summary>
        /// <param name="highestPriority">the highest priority that is processed by this server</param>
        /// <param name="lowestPriority">the lowest priority that is processed by this server</param>
        /// <param name="workerCount">the number of workers to use</param>
        /// <param name="workerPollInterval">the interval after which a worker will poll the working queue if it has not been triggered</param>
        /// <param name="lowTaskThreshold">the minimum number of tasks that the workerqueue should contain</param>
        /// <param name="highTaskThreshold">the maximum number of tasks that the workerqueue should contain</param>
        /// <param name="useAffineThreads">indicates whether to use affine threads</param>
        /// <param name="eventReTriggerInterval">the timeout in minutes after which an event that has not been commited by a client is re-triggered</param>
        /// <param name="maximumFailsPerItem">defines how many times a task can fail before it is considered a failure</param>
        /// <param name="factory">the factory that is used to initialize further plugins</param>
        protected ParallelServer(int highestPriority, int lowestPriority, int workerCount, int workerPollInterval, int lowTaskThreshold, int highTaskThreshold, bool useAffineThreads, int eventReTriggerInterval, int maximumFailsPerItem, PluginFactory factory) : this()
        {
            packages = new Dictionary<int, ConcurrentBag<TPackage>>();
            for (int i = highestPriority; i <= lowestPriority; i++)
            {
                packages.Add(i, new ConcurrentBag<TPackage>());
            }

            this.highestPriority = highestPriority;
            this.lowestPriority = lowestPriority;
            this.workerCount = workerCount;
            this.workerPollInterval = workerPollInterval;
            this.lowTaskThreshold = lowTaskThreshold;
            this.highTaskThreshold = highTaskThreshold;
            this.useAffineThreads = useAffineThreads;
            this.eventReTriggerInterval = eventReTriggerInterval;
            this.maximumFailsPerItem = maximumFailsPerItem;
            this.factory = factory;
            eventReTriggerer.Change(10000, 10000);
        }

        /// <summary>
        /// Prevents a default instance of the ParallelServer class from being created
        /// </summary>
        private ParallelServer()
        {
            workingPackages = new List<TPackage>();
            triggeredEvents = new List<PackageFinishedEventArgsReTriggerContainer>();
            eventReTriggerer = new Timer(RetriggerEvents, string.Format("::{0}::", GetHashCode()), Timeout.Infinite,
                                         Timeout.Infinite);
            statData = new StatisticData();
        }

        /// <summary>
        /// Gets or sets the UniqueName of this Plugin
        /// </summary>
        public abstract string UniqueName { get; set; }

        /// <summary>
        /// Indicates whether this deferrable init-object is already initialized
        /// </summary>
        public bool Initialized { get; private set; }

        /// <summary>
        /// Indicates whether this Object requires immediate Initialization right after calling the constructor
        /// </summary>
        public bool ForceImmediateInitialization => true;

        /// <summary>
        /// Gets the processor that is internally used to process packages.
        /// </summary>
        protected ParallelTaskProcessor<TTask> Processor { get { return processor; } }

        /// <summary>
        /// Initializes this deferred initializable object
        /// </summary>
        public void Initialize()
        {
            if (!Initialized)
            {
                try
                {
                    Ready();
                    Init();
                }
                finally
                {
                    Initialized = true;
                }
            }
        }

        /// <summary>
        /// Führt anwendungsspezifische Aufgaben durch, die mit der Freigabe, der Zurückgabe oder dem Zurücksetzen von nicht verwalteten Ressourcen zusammenhängen.
        /// </summary>
        /// <filterpriority>2</filterpriority>
        public virtual void Dispose()
        {
            processor.Dispose();
        }

        /// <summary>
        /// Stops Processes that are running inside the current Plugin
        /// </summary>
        public virtual void Stop()
        {
            processor.Stop();
        }

        /// <summary>
        /// Gets the Runtime information required to restore the status when the application restarts
        /// </summary>
        /// <returns>an object serializer containing all required data for object re-construction on application reboot</returns>
        public virtual RuntimeInformation GetPostDisposalSerializableStaus()
        {
            RuntimeInformation retVal = new RuntimeInformation();
            retVal.Add("processor", processor.GetPostDisposalSerializableStaus());
            for (int i = highestPriority; i <= lowestPriority; i++)
            {
                retVal.Add(string.Format("priority_{0}_packages", i), packages[i].ToArray());
            }

            retVal.Add("WorkingPackages", workingPackages.ToArray());
            retVal.Add("TriggeredEvents", triggeredEvents.ToArray());
            return retVal;
        }

        /// <summary>
        /// Enables a client to commit that it recieved the TaskDone event for a specific package
        /// </summary>
        /// <param name="requestingSystem">the identifier of the requesting system</param>
        /// <param name="packageId">the package identifier for that system</param>
        public void CommitTaskDoneRecieved(string requestingSystem, int packageId)
        {
            lock (triggeredEvents)
            {
                LogEnvironment.LogDebugEvent(string.Format(ParallellResources.ParallelServer_CommitTaskDoneRecieved_System__0__confirms_that_TaskDone_of_the_Task__1__was_received_,
                                         requestingSystem, packageId), LogSeverity.Report);
                var commitedEvent = (from t in triggeredEvents
                                     where
                                         t.Args.Package.RequestingSystem == requestingSystem &&
                                         t.Args.Package.Id == packageId
                                     select t).ToArray();
                if (commitedEvent.Length != 1)
                {
                    LogEnvironment.LogDebugEvent(ParallellResources.ParallelServer_Unable_to_commit_this_event_, LogSeverity.Warning);
                    return;
                }

                triggeredEvents.Remove(commitedEvent[0]);
            }
        }

        /// <summary>
        /// Applies Runtime information that was loaded from a file
        /// </summary>
        /// <param name="runtimeInformation">the runtime information describing the status of this object before the last shutdown</param>
        public virtual void LoadRuntimeStatus(RuntimeInformation runtimeInformation)
        {
            for (int i = highestPriority; i <= lowestPriority; i++)
            {
                TPackage[] packsI = (TPackage[]) runtimeInformation[string.Format("priority_{0}_packages", i)];
                foreach (TPackage package in packsI)
                {
                    IntegratePackage(package, PackageReintegrationStatus.Pending);
                    packages[i].Add(package);
                }
            }

            foreach (TPackage package in (TPackage[]) runtimeInformation["WorkingPackages"])
            {
                package.PackageFinished += PackageFinished;
                package.DemandForRequeue += DemandForRequeue;
                IntegratePackage(package, PackageReintegrationStatus.Processing);
                workingPackages.Add(package);
            }

            foreach (
                PackageFinishedEventArgsReTriggerContainer container in
                    (PackageFinishedEventArgsReTriggerContainer[]) runtimeInformation["TriggeredEvents"])
            {
                IntegratePackage((TPackage)container.Args.Package, PackageReintegrationStatus.Done);
                triggeredEvents.Add(container);
            }

            processor.LoadRuntimeStatus((RuntimeInformation)runtimeInformation["processor"]);
        }

        /// <summary>
        /// Enqueues a package into the list of processable packages
        /// </summary>
        /// <param name="package">the package that requires processing</param>
        [UserDelegation("enqueueUser")]
        public void EnqueuePackage(TPackage package)
        {
            IIdentity identity = package.LocalConfiguration<IIdentity>("enqueueUser");
            if (!CheckAuthenticatedUser(identity))
            {
                throw new InvalidOperationException("You are not authorized to perform the demanded Action");
            }

            ConcurrentBag<TPackage> target = packages[package.PackagePriority];
            IntegratePackage(package, PackageReintegrationStatus.Pending);
            target.Add(package);
        }

        /// <summary>
        /// Enqueues a package into the list of processable packages
        /// </summary>
        /// <param name="package">the package that requires processing</param>
        [UserDelegation("enqueueUser")]
        public void EnqueuePackage(IProcessPackage package)
        {
            IIdentity identity = package.LocalConfiguration<IIdentity>("enqueueUser");
            if (!CheckAuthenticatedUser(identity))
            {
                throw new InvalidOperationException("You are not authorized to perform the demanded Action");
            }

            if (package is TPackage)
            {
                EnqueuePackage((TPackage) package);
            }
        }

        /// <summary>
        /// Requeues a task to the worker queue
        /// </summary>
        /// <param name="task">the task that needs to be processed by the target queue</param>
        protected bool RequeueTask(TTask task)
        {
            return processor.EnqueueTask(task);
        }

        /// <summary>
        /// Allows this object to do required initializations when no runtime status is provided by the calling object
        /// </summary>
        public virtual void InitializeWithoutRuntimeInformation()
        {
            processor.InitializeWithoutRuntimeInformation();
        }

        /// <summary>
        /// Is called when the runtime is completly available and ready to run
        /// </summary>
        public void RuntimeReady()
        {
            processor.RuntimeReady();
        }

        /// <summary>
        /// Instructs a IMetricsProvider Implementing object to start gathering System metrics
        /// </summary>
        /// <returns>a value indicating whether the starting of metrincs-gathering was successful</returns>
        public bool BeginCollectStatistics()
        {
            lock (this)
            {
                collectStats = true;
            }

            return true;
        }

        /// <summary>
        /// Instructs a IMetricsProvider implementing object to end the System metrics gathering
        /// </summary>
        /// <returns>indicates whether the stopping of the gathering process was successful</returns>
        public bool EndCollectStatistics()
        {
            lock (this)
            {
                collectStats = false;
            }

            return true;
        }

        /// <summary>
        /// Resets the previously collected statistic data
        /// </summary>
        public void ResetStats()
        {
            while (!statData.ProcessingData.IsEmpty)
            {
                ProcessedPackageInfo info;
                statData.ProcessingData.TryTake(out info);
            }

            ResetSpecialStatistics();
        }

        /// <summary>
        /// Gets statistics that were collected since the call of BeginCollectStatistics
        /// </summary>
        /// <returns>a key-value set containing informations about the runtime-status of this object</returns>
        public Dictionary<string, object> GetStatistics()
        {
            Dictionary<string, object> retVal = new Dictionary<string, object>();
            BuildSpecialStatistics(retVal);
            retVal["TotalProcessedJobs"] = statData.ProcessingData.Count;
            retVal["TotalProcessedItems"] = statData.ProcessingData.Sum(n => n.ItemCount);
            retVal["TotalErrorCount"] = statData.ProcessingData.Sum(n => n.FailCount);
            retVal["AvgItemCount"] = statData.ProcessingData.Count != 0
                                         ? statData.ProcessingData.Average(n => n.ItemCount)
                                         : 0;
            retVal["AvgFailRate"] = string.Format("{0:0.00}%",
                                                  statData.ProcessingData.Count != 0
                                                      ? statData.ProcessingData.Average(
                                                          n => (double) n.FailCount/(double) n.ItemCount)*100
                                                      : 0);
            retVal["AvgJobDuration"] = statData.ProcessingData.Count != 0
                                           ? statData.ProcessingData.Average(n => n.Duration)
                                           : 0;
            retVal["AvgItemsPerSecond"] = statData.ProcessingData.Count != 0
                                              ? statData.ProcessingData.Average(n => n.ItemCount/n.Duration)
                                              : 0;
            foreach (KeyValuePair<int, ConcurrentBag<TPackage>> queue in packages)
            {
                retVal[string.Format("WaitingTasksPriority{0}", queue.Key)] = queue.Value.Count;
            }

            lock (triggeredEvents)
            {
                retVal["uncommittedEvents"] = triggeredEvents.Count;
            }

            lock (workingPackages)
            {
                retVal["workingPackages"] = workingPackages.Count;
            }

            return retVal;
        }

        /// <summary>
        /// Enables a derived class to provide specialized statistic information that may be relevant to this processor object
        /// </summary>
        /// <param name="statistics">the statistics object to which general statistic information will be added after the special statistic information is built</param>
        protected virtual void BuildSpecialStatistics(Dictionary<string, object> statistics)
        {
        }

        /// <summary>
        /// Enables a derived object to reset its collected statisticdata before a job is started
        /// </summary>
        protected virtual void ResetSpecialStatistics()
        {
        }

        /// <summary>
        /// Runs Initializations on derived objects
        /// </summary>
        protected virtual void Init()
        {
        }

        /// <summary>
        /// Override this method if only specific users may be authenticated to use the Enqueue - Methods of this Server
        /// </summary>
        /// <param name="identity">the identity that was provided by the caller</param>
        /// <returns>a value indicating whether the current user is allowed to perform enqueue tasks</returns>
        protected virtual bool CheckAuthenticatedUser(IIdentity identity)
        {
            return true;
        }

        /// <summary>
        /// Enables a derived class to perform required actions to integrate a pending task into the current environment
        /// </summary>
        /// <param name="task">the task that needs to be integrated into the running environment</param>
        protected virtual void IntegrateTask(TTask task)
        {
        }

        /// <summary>
        /// Enables a derived class to perform required actions to integrate packages that are either pending or in progress or done
        /// </summary>
        /// <param name="package">the package that needs to be integrated into the running environment</param>
        protected virtual void IntegratePackage(TPackage package, PackageReintegrationStatus status)
        {
        }

        /// <summary>
        /// Creates a new worker instance when requested by one of the workers
        /// </summary>
        /// <returns>a worker that is capable for processing tasks generated by this Server instance</returns>
        protected abstract ITaskWorker<TTask> GetWorker();

        /// <summary>
        /// Raises the Disposed event
        /// </summary>
        protected void OnDisposed()
        {
            EventHandler handler = Disposed;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        /// <summary>
        /// Enables a derived class to collect further statistic information after the general statistic information was collected
        /// </summary>
        /// <param name="packageFinishedEventArgs">information about a finished package</param>
        protected virtual void CollectSpecialPackageStatistics(PackageFinishedEventArgs packageFinishedEventArgs)
        {
        }

        /// <summary>
        /// Use this method to initialize the ParallelProcessor unit when your derived class has initialized successfully
        /// </summary>
        private void Ready()
        {
            string workerName = this.UniqueName + "TaskProcessor";
            processor = new ParallelTaskProcessor<TTask>(workerName,GetWorker, highestPriority, lowestPriority, workerCount,
                workerPollInterval, lowTaskThreshold, highTaskThreshold,
                useAffineThreads, watchDog);
            processor.GetMoreTasks += FetchMoreTasks;
            processor.IntegratePendingTask += IntegratePendingTasks;
            factory.RegisterObject(workerName, processor);
        }

        /// <summary>
        /// Fetches tasks that are waiting to be processed
        /// </summary>
        /// <param name="sender">the event-sender</param>
        /// <param name="e">event information</param>
        private void FetchMoreTasks(object sender, GetMoreTasksEventArgs e)
        {
            TPackage package;
            if (packages[e.Priority].TryTake(out package))
            {
                IProcessTask[] items = package.GetTasks();
                lock (workingPackages)
                {
                    package.PackageFinished += PackageFinished;
                    package.DemandForRequeue += DemandForRequeue;
                    workingPackages.Add(package);
                }

                bool[] ok = new bool[items.Length];
                while (ok.Any(n => !n))
                {
                    for (int index = 0; index < items.Length; index++)
                    {
                        IProcessTask item = items[index];
                        ok[index] = processor.EnqueueTask((TTask) item);
                    }
                }
            }
        }

        /// <summary>
        /// Integrates deserialized tasks into the runtime environment of the current application
        /// </summary>
        /// <param name="sender">the event-sender</param>
        /// <param name="e">the integration arguments containing the package that needs to be integrated</param>
        private void IntegratePendingTasks(object sender, IntegrationEventArgs e)
        {
            IntegrateTask((TTask) e.Task);
        }

        /// <summary>
        /// Triggers events that have not been commited yet
        /// </summary>
        /// <param name="state">unused state object</param>
        private void RetriggerEvents(object state)
        {
            eventReTriggerer.Change(Timeout.Infinite, Timeout.Infinite);
            state.LocalOwner(state.ToString());
            try
            {
                lock (triggeredEvents)
                {
                    foreach (
                        var arg in
                            (from t in triggeredEvents
                             where DateTime.Now.Subtract(t.LastTrigger).TotalMinutes > eventReTriggerInterval
                             select t))
                    {
                        arg.LastTrigger = DateTime.Now;
                        if (PackageProcessed != null)
                        {
                            PackageProcessed(this, arg.Args);
                        }
                    }
                }
            }
            finally
            {
                eventReTriggerer.Change(eventReTriggerInterval, eventReTriggerInterval);
                state.LocalOwner(null);
            }
        }

        /// <summary>
        /// Handles the PackageFinished event of a processed package
        /// </summary>
        /// <param name="sender">the event-sender</param>
        /// <param name="e">arguments about the finished package</param>
        private void PackageFinished(object sender, PackageFinishedEventArgs e)
        {
            e.Package.PackageFinished -= PackageFinished;
            e.Package.DemandForRequeue -= DemandForRequeue;
            lock (triggeredEvents)
            {
                triggeredEvents.Add(new PackageFinishedEventArgsReTriggerContainer
                                        {
                                            Args = e,
                                            LastTrigger = DateTime.Now
                                        });
            }

            if (PackageProcessed != null)
            {
                PackageProcessed(this, e);
            }
            bool collect;
            lock (this)
            {
                collect = collectStats;
            }
            if (collect)
            {
                ProcessedPackageInfo info = new ProcessedPackageInfo();
                info.Duration = DateTime.Now.Subtract(e.Package.CreationTime).TotalSeconds;
                info.ItemCount = e.Tasks.Length;
                info.SuccessCount = (from i in e.Tasks where i.Success select i).Count();
                info.FailCount = (from i in e.Tasks select i.FailCount).Sum();
                CollectSpecialPackageStatistics(e);
                statData.ProcessingData.Add(info);
            }
            lock (workingPackages)
            {
                workingPackages.Remove((TPackage)e.Package);
            }
        }

        /// <summary>
        /// Requeues a task if supported by the current configuration and state of the task that wants to be re-queued
        /// </summary>
        /// <param name="sender">the event-sender</param>
        /// <param name="target">the target task that requires re-queueing</param>
        /// <returns>a value indicating whether the re-queueing is supported for this task</returns>
        private bool DemandForRequeue(object sender, IProcessTask target)
        {
            bool retVal = false;
            if (target.FailCount < maximumFailsPerItem)
            {
                retVal = processor.EnqueueTask((TTask)target);
            }

            return retVal;
        }

        /// <summary>
        /// Informs listening clients that a package that has been passed for processing is done
        /// </summary>
        public event PackageFinishedEventHandler PackageProcessed;

        /// <summary>
        /// Informs a calling class of a Disposal of this Instance
        /// </summary>
        public event EventHandler Disposed;

        private class StatisticData
        {
            public StatisticData()
            {
                ProcessingData = new ConcurrentBag<ProcessedPackageInfo>();
            }

            public ConcurrentBag<ProcessedPackageInfo> ProcessingData { get; private set; }
        }

        private class ProcessedPackageInfo
        {
            /// <summary>
            /// Gets or sets the number of items that has been processed by this item
            /// </summary>
            public int ItemCount { get; set; }

            /// <summary>
            /// Gets or sets the number of items that have been processed successfully
            /// </summary>
            public int SuccessCount { get; set; }

            /// <summary>
            /// Gets or sets the number of items that have failed
            /// </summary>
            public int FailCount { get; set; }

            /// <summary>
            /// Gets or setse the total duration in seconds
            /// </summary>
            public double Duration { get; set; }
        }
    }
}
