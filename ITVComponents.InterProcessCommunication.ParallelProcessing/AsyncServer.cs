using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Authentication;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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
    public abstract class AsyncServer<TPackage, TTask>: IAsyncServer, IStatisticsProvider, IStoppable, IDeferredInit
                                                                      where TTask:IProcessTask 
                                                                      where TPackage:IProcessPackage 
    {
        /// <summary>
        /// TaskProcessor class that is used to process the provided items coming from the client
        /// </summary>
        private ParallelTaskProcessor<TTask> processor;

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

        private readonly bool useTasks;

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
        /// <param name="factory">the factory that is used to initialize further plugins</param>
        protected AsyncServer(int highestPriority, int lowestPriority, int workerCount, int workerPollInterval, int lowTaskThreshold,
                                 int highTaskThreshold, bool useAffineThreads, bool useTasks, PluginFactory factory)
            : this(
                highestPriority, lowestPriority, workerCount, workerPollInterval, lowTaskThreshold, highTaskThreshold, useAffineThreads, 0, useTasks, factory)
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
        /// <param name="factory">the factory that is used to initialize further plugins</param>
        /// <param name="watchDog">a watchdog instance that is used to restart non-responsive processors</param>
        protected AsyncServer(int highestPriority, int lowestPriority, int workerCount, int workerPollInterval, int lowTaskThreshold,
            int highTaskThreshold, bool useAffineThreads, bool useTasks, PluginFactory factory, WatchDog watchDog)
            : this(
                highestPriority, lowestPriority, workerCount, workerPollInterval, lowTaskThreshold, highTaskThreshold, useAffineThreads, 0, useTasks, factory, watchDog)
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
        /// <param name="maximumFailsPerItem">defines how many times a task can fail before it is considered a failure</param>
        /// <param name="factory">the factory that is used to initialize further plugins</param>
        /// <param name="watchDog">a watchdog instance that is used to restart non-responsive processors</param>
        protected AsyncServer(int highestPriority, int lowestPriority, int workerCount, int workerPollInterval, int lowTaskThreshold, int highTaskThreshold, bool useAffineThreads, int maximumFailsPerItem, bool useTasks, PluginFactory factory, WatchDog watchDog) :
            this(highestPriority, lowestPriority, workerCount, workerPollInterval, lowTaskThreshold, highTaskThreshold, useAffineThreads, maximumFailsPerItem, useTasks, factory)
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
        /// <param name="maximumFailsPerItem">defines how many times a task can fail before it is considered a failure</param>
        /// <param name="factory">the factory that is used to initialize further plugins</param>
        protected AsyncServer(int highestPriority, int lowestPriority, int workerCount, int workerPollInterval, int lowTaskThreshold, int highTaskThreshold, bool useAffineThreads, int maximumFailsPerItem, bool useTasks, PluginFactory factory) : this()
        {
            this.highestPriority = highestPriority;
            this.lowestPriority = lowestPriority;
            this.workerCount = workerCount;
            this.workerPollInterval = workerPollInterval;
            this.lowTaskThreshold = lowTaskThreshold;
            this.highTaskThreshold = highTaskThreshold;
            this.useAffineThreads = useAffineThreads;
            this.maximumFailsPerItem = maximumFailsPerItem;
            this.useTasks = useTasks;
            this.factory = factory;
        }

        /// <summary>
        /// Prevents a default instance of the ParallelServer class from being created
        /// </summary>
        private AsyncServer()
        {
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
        /// Enqueues a package into the list of processable packages
        /// </summary>
        /// <param name="package">the package that requires processing</param>
        [UserDelegation("enqueueUser")]
        public async Task<TPackage> EnqueuePackage(TPackage package)
        {
            IIdentity identity = package.LocalConfiguration<IIdentity>("enqueueUser");
            if (!CheckAuthenticatedUser(identity))
            {
                throw new InvalidOperationException("You are not authorized to perform the demanded Action");
            }

            package.DemandForRequeue += DemandForRequeue;
            try
            {
                await Task.WhenAll(package.GetTasks().Cast<TTask>().Select(n => processor.ProcessAsync(n)));
            }
            finally
            {
                package.DemandForRequeue -= DemandForRequeue;
            }
            
            return package;
        }

        /// <summary>
        /// Enqueues a package into the list of processable packages
        /// </summary>
        /// <param name="package">the package that requires processing</param>
        [UserDelegation("enqueueUser")]
        public async Task<IProcessPackage> EnqueuePackage(IProcessPackage package)
        {
            IIdentity identity = package.LocalConfiguration<IIdentity>("enqueueUser");
            if (!CheckAuthenticatedUser(identity))
            {
                throw new InvalidOperationException("You are not authorized to perform the demanded Action");
            }

            if (package is TPackage pack)
            {
                return await EnqueuePackage(pack);
            }

            throw new InvalidOperationException("This package type can not be processed by this Server");
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
                useAffineThreads, useTasks, watchDog);
            factory.RegisterObject(workerName, processor);
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
