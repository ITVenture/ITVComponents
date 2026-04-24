using ITVComponents.Logging;
using ITVComponents.ParallelProcessing;
using ITVComponents.Plugins;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.StateMachine.Parallel
{
    public abstract class AsyncStatusProcessor: IStoppable, IDeferredInit, IPlugin
    {
        /// <summary>
        /// the exponent used to get an accurate task priority scheduling
        /// </summary>
        private const int PriorityExponent = 3;

        private readonly int highestPriority;
        private readonly int lowestPriority;
        private readonly int lowTaskThreshold;
        private readonly int highTaskThreshold;
        private readonly int workerCount;
        private ManualResetEvent stop;
        private ManualResetEvent stopped;
        private ManualResetEvent pollerFree;
        private List<IStatusWorker> workers = new List<IStatusWorker>();
        public string UniqueName { get; set; }
        public bool Initialized { get; private set; }
        public bool ForceImmediateInitialization { get; protected set; }

        /// <summary>
        /// indicates whether the GetMoreJobs event needs to be triggered
        /// </summary>
        private Dictionary<int, bool> filling;

        private Dictionary<int, ConcurrentQueue<ITask>> tasks;
        private bool stopCalled;

        protected AsyncStatusProcessor(int highestPriority, int lowestPriority, int lowTaskThreshold, int highTaskThreshold, int workerCount)
        {
            this.highestPriority = highestPriority;
            this.lowestPriority = lowestPriority;
            this.lowTaskThreshold = lowTaskThreshold;
            this.highTaskThreshold = highTaskThreshold;
            this.workerCount = workerCount;
            tasks = new Dictionary<int, ConcurrentQueue<ITask>>();
            filling = new Dictionary<int, bool>();
            if (highestPriority > lowestPriority)
            {
                throw new ArgumentException("Highest priority must be smaller than lowest priority");
            }

            for (int i = highestPriority; i <= lowestPriority; i++)
            {
                tasks.Add(i, new ConcurrentQueue<ITask>());
                filling.Add(i, false);
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

                    //lo = lo > highestPriority ? lo-1 : lo;
                    lo = lo > highestPriority ? lo - 1 : lowestPriority;
                    exclusiveCount = (lowestPriority - lo) + 1;
                }
            }

            if (lo != highestPriority && !roundClock)
            {
                LogEnvironment.LogEvent("There are not enough Workers available to have exclusive high-prio workers. High Priority Tasks may take longer than expected", LogSeverity.Warning);
                //CreateProcessor(highestPriority);
            }
        }

        private void CreateProcessor(int startPriority)
        {
            workers.Add(new AsyncStatusWorker(stop, this, BuildQueueList(startPriority)));
        }

        public void Initialize()
        {
            if (stopCalled)
            {
                throw new InvalidOperationException("AsyncStatusProcessor can not be restarted after stop.");
            }

            if (!Initialized)
            {
                try
                {
                    stop = new ManualResetEvent(false);
                    stopped = new ManualResetEvent(false);
                    pollerFree = new ManualResetEvent(true);
                    ThreadPool.RegisterWaitForSingleObject(stop, WaitForStop, null, 100, false);
                    workers.ForEach(n=>n.Initialize());
                }
                finally
                {
                    Initialized = true;
                }
            }
        }

        private List<ConcurrentQueue<ITask>> BuildQueueList(int startPriority)
        {
            var processCycle = new List<ConcurrentQueue<ITask>>();
            Random rnd = new Random();
            List<KeyValuePair<int, int>> l = new List<KeyValuePair<int, int>>();
            int priorityCount = (startPriority - highestPriority) + 1;
            for (int i = highestPriority, a = 0; i <= startPriority; i++, a++)
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

                processCycle.Add(tasks[i]);
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

            return processCycle;
        }

        private void WaitForStop(object? state, bool timedOut)
        {
            if (timedOut)
            {
                if (pollerFree.WaitOne(5))
                {
                    pollerFree.Reset();
                    try
                    {
                        foreach (var kv in tasks)
                        {
                            if (kv.Value.Count >= highTaskThreshold && filling[kv.Key])
                            {
                                filling[kv.Key] = false;
                            }

                            if (kv.Value.Count <= lowTaskThreshold && !filling[kv.Key])
                            {
                                filling[kv.Key] = true;
                            }

                            if (filling[kv.Key])
                            {
                                OnGetMoreTasks(kv.Key, highTaskThreshold - kv.Value.Count);
                            }
                        }
                    }
                    finally
                    {
                        pollerFree.Set();
                    }
                }
            }
            else
            {
                stopped.Set();
            }
        }

        public void Stop()
        {
            if (!stopCalled)
            {
                stop.Set();
                stopped.WaitOne();
                stopCalled = true;
            }
        }

        public void Dispose()
        {
            Stop();
            Dispose(true);
            workers.ForEach(n => n.Dispose());
            OnDisposed();
        }

        public void HandleWorkerException(Exception exception, ITask task = null)
        {
            HandleWorkerExceptionInternal(exception, task);
        }

        protected virtual void HandleWorkerExceptionInternal(Exception exception, ITask task)
        {
            LogEnvironment.LogEvent($"An exception occurred in AsyncStatusProcessor: {exception.Message}", LogSeverity.Error);
        }

        protected void Dispose(bool disposing)
        {
        }

        protected virtual void OnDisposed()
        {
            Disposed?.Invoke(this, EventArgs.Empty);
        }

        protected virtual void OnGetMoreTasks(int priority, int taskCount)
        {
            if (!stopCalled)
            {
                GetMoreTasks?.Invoke(this, new GetMoreTasksEventArgs
                {
                    TaskCount = taskCount,
                    Priority = priority
                });
            }
        }

        public event EventHandler? Disposed;

        /// <summary>
        /// Triggers a client object to provide more tasks that need to be executed.
        /// </summary>
        public event EventHandler<GetMoreTasksEventArgs> GetMoreTasks;
    }
}
