using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Security.Permissions;
using System.Text;
using System.Threading;
using ITVComponents.Logging;
namespace ITVComponents.Threading
{
    /// <summary>
    /// Works background jobs in a single thread
    /// </summary>
    public class BackgroundRunner
    {
        /// <summary>
        /// A List of tasks that need to be executed
        /// </summary>
        //private static ConcurrentQueue<Action> tasks;

        /// <summary>
        /// a list of tasks that need to be executed in every period if this worker
        /// </summary>
        private static List<ActionPackage> subSequentTasks; 

        /// <summary>
        /// The worker thread that is used to execute all the tasks
        /// </summary>
        private static Thread worker;

        /// <summary>
        /// Informs the worker thread for new work
        /// </summary>
        private static ManualResetEvent workToDo;

        /// <summary>
        /// Quits the worker thread
        /// </summary>
        private static ManualResetEvent exit;

        /// <summary>
        /// indicates whether this BackgroundRunner is initialized
        /// </summary>
        private static bool initialized = false;

        /// <summary>
        /// Lock object to avoid multiple initializations
        /// </summary>
        private static object initializer = new object();

        /// <summary>
        /// the time when the worker actually noticed activity
        /// </summary>
        private static DateTime lastActualUsage = DateTime.Now;

        /// <summary>
        /// Enqueues a single task that needs to be done
        /// </summary>
        /// <param name="action">the action that needs to be executed</param>
        public static void EnqueueSingleTask(Action action)
        {
            ThreadPool.QueueUserWorkItem(o =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    LogEnvironment.LogEvent(ex.ToString(), LogSeverity.Error);
                }
            });
        }

        /// <summary>
        /// Adds a task that must be executed in every loop
        /// </summary>
        /// <param name="action">the action to be executed</param>
        /// <param name="periodicy">the periodicy indicating after how many milliseconds a task will be triggered again</param>
        public static void AddPeriodicTask(Action action, int periodicy)
        {
            if (!initialized)
            {
                Initialize();
            }

            lock (subSequentTasks)
            {
                subSequentTasks.Add(new ActionPackage()
                                        {
                                            Action = action,
                                            LastExecution = DateTime.MinValue,
                                            TimeoutInMilliseconds = periodicy,
                                            Queued = false
                                        });
            }
        }

        /// <summary>
        /// Removes a task from the list of periodic tasks
        /// </summary>
        /// <param name="action">the action to be removed</param>
        public static void RemovePeriodicTask(Action action)
        {
            lock (subSequentTasks)
            {
                var tmp = subSequentTasks.Where(n => n.Action == action).ToArray();
                Array.ForEach(tmp,n => subSequentTasks.Remove(n));
                if (subSequentTasks.Count == 0)
                {
                    exit.Set();
                    initialized = false;
                }
                //subSequentTasks.Remove(action);
            }
        }

        /// <summary>
        /// Forces stopping the worker loop
        /// </summary>
        public static void ForceStop()
        {
            if (exit != null)
            {
                exit.Set();
            }
        }

        /// <summary>
        /// Initializes the Background TaskRunner
        /// </summary>
        private static void Initialize()
        {
            lock (initializer)
            {
                if (!initialized)
                {
                    lastActualUsage = DateTime.Now;
                    bool fullInitialization = worker == null;

                    worker = new Thread(Work);
                    if (fullInitialization)
                    {
                        subSequentTasks = new List<ActionPackage>();
                        exit = new ManualResetEvent(false);
                        AppDomain.CurrentDomain.ProcessExit += Quit;
                    }

                    worker.Start();
                    initialized = true;
                }
            }
        }

        /// <summary>
        /// Works tasks that need to be done
        /// </summary>
        private static void Work()
        {
            lock (initializer)
            {
                exit.Reset();
            }

            while (!exit.WaitOne(50))
            {
                bool foundTask = false;

                lock (subSequentTasks)
                {
                    foundTask |= subSequentTasks.Count != 0;
                    foreach (
                        ActionPackage a in
                        subSequentTasks.Where(
                            n =>
                                DateTime.Now.Subtract(n.LastExecution).TotalMilliseconds >
                                n.TimeoutInMilliseconds && !n.Queued))
                    {
                        a.Queued = true;
                        ThreadPool.QueueUserWorkItem(o =>
                        {
                            try
                            {
                                a.Action();
                            }
                            catch (Exception ex)
                            {
                                LogEnvironment.LogEvent(ex.ToString(), LogSeverity.Error);
                            }
                            finally
                            {
                                a.LastExecution = DateTime.Now;
                                a.Queued = false;
                            }
                        });
                    }
                }

                if (!foundTask)
                {
                    lock (initializer)
                    {
                        DateTime now = DateTime.Now;
                        if (now.Subtract(lastActualUsage).TotalSeconds > 10)
                        {
                            initialized = false;
                            break;
                        }
                    }

                    continue;
                }

                lastActualUsage = DateTime.Now;
            }
        }

        /// <summary>
        /// Sets the Exit event
        /// </summary>
        /// <param name="sender">not required</param>
        /// <param name="e">not required</param>
        private static void Quit(object sender, EventArgs e)
        {
            LogEnvironment.LogDebugEvent(null, "Application is trying to exit...", (int) LogSeverity.Report, null);
            exit.Set();
        }

        private class ActionPackage
        {
            public Action Action { get; set; }

            public int TimeoutInMilliseconds { get; set; }

            public DateTime LastExecution { get; set; }

            public bool Queued { get; set; }
        }
    }
}
