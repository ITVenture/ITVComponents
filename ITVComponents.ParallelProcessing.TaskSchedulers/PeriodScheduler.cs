using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ITVComponents.DataAccess.Extensions;
using ITVComponents.Logging;
using ITVComponents.ParallelProcessing.TaskSchedulers.Requests;
using ITVComponents.Plugins.SelfRegistration;
using ITVComponents.Serialization;
using ITVComponents.Threading;

namespace ITVComponents.ParallelProcessing.TaskSchedulers
{
    /// <summary>
    /// Provides Scheduling capabilities for periodic tasks
    /// </summary>
    public class PeriodScheduler : TaskScheduler
    {
        /// <summary>
        /// Holds a list of pending requests waiting in this scheduler
        /// </summary>
        private List<PeriodScheduleRequest> pendingRequests;

        /// <summary>
        /// Contains all timetables that are currently active for scheduling events
        /// </summary>
        private static ConcurrentDictionary<string, TimeTable> timeTables = new ConcurrentDictionary<string, TimeTable>();

        /// <summary>
        /// Initializes a new instance of the PeriodScheduler
        /// </summary>
        public PeriodScheduler()
            : base()
        {
            pendingRequests = new List<PeriodScheduleRequest>();
        }

        /// <summary>
        /// Indicates whether a derived Scheduler supports pushing requests to run immediate
        /// </summary>
        public override bool CanPushTasks
        {
            get { return true; }
        }

        /// <summary>
        /// On a derived class, this Method can push an SchedulingRequest, so it will be executed immediately
        /// </summary>
        /// <param name="requestId">the unique id of the request that requires immediate execution</param>
        /// <returns>a value indicating whether the request was found and pushing was successful</returns>
        public override bool PushRequest(string requestId)
        {
            bool retVal = false;
            lock (pendingRequests)
            {
                if (pendingRequests.Any(n=>n.RequestId==requestId))
                {
                    PeriodScheduleRequest request = pendingRequests.First(n=>n.RequestId==requestId);
                    pendingRequests.Remove(request);
                    request.Task.LastExecution = DateTime.Now;
                    base.ScheduleTask(request);
                    retVal = true;
                }
            }

            return retVal;
        }

        /// <summary>
        /// Creates a Task request for this TaskScheduler with the Task that needs to be executed and the queue that will execute it
        /// </summary>
        /// <param name="parallelTaskProcessor">the TaskProcessor that will run the provided task later</param>
        /// <param name="task">the task that needs to be executed</param>
        /// <param name="instruction">the default instruction for this schedule request</param>
        /// <returns>a scheduler - request for the given processor and task</returns>
        public override ScheduleRequest CreateRequest(ParallelTaskProcessor parallelTaskProcessor, ITask task)
        {
            PeriodScheduleRequest retVal = new PeriodScheduleRequest(UniqueName, parallelTaskProcessor, task, this, task.LastExecution);
            return retVal;
        }

        /// <summary>
        /// Schedules a task for execution
        /// </summary>
        /// <param name="task">the task that needs to be executed</param>
        /// <returns>a value indicating whether the task was scheduled</returns>
        public override bool ScheduleTask(TaskScheduler.ScheduleRequest task)
        {
            PeriodScheduleRequest schereq = task as PeriodScheduleRequest;
            if (schereq == null)
            {
                return false;
            }

            /*if (task.Task.SchedulerInstruction == null)
            {
                return base.ScheduleTask(task);
            }*/

            DateTime? next = schereq.NextExecution;
            if (next == null)
            {
                return false;
            }

            if (next.Value > DateTime.Now)
            {
                lock (pendingRequests)
                {
                    pendingRequests.Add(schereq);
                }
            }
            else
            {
                task.Task.LastExecution = DateTime.Now;
                return base.ScheduleTask(task);
            }

            return true;
        }

        /// <summary>
        /// Gets a list of available Schedule - Requests on this scheduler
        /// </summary>
        /// <returns>an array of tasks that are waiting to be scheduled</returns>
        public override TaskScheduler.ScheduleRequest[] GetWaitingTasks()
        {
            lock (pendingRequests)
            {
                return pendingRequests.ToArray();
            }
        }

        /// <summary>
        /// Führt anwendungsspezifische Aufgaben durch, die mit der Freigabe, der Zurückgabe oder dem Zurücksetzen von nicht verwalteten Ressourcen zusammenhängen.
        /// </summary>
        /// <filterpriority>2</filterpriority>
        public override void Dispose()
        {
            BackgroundRunner.RemovePeriodicTask(CheckSchedules);
            base.Dispose();
        }

        /// <summary>
        /// Gets a specific timeTable for a given instructionset
        /// </summary>
        /// <param name="instruction">the instruction set to describe a periodic task</param>
        /// <returns>a timetable representing all upcoming events for a specific task</returns>
        internal static TimeTable GetTimeTable(string instruction)
        {
            return timeTables.GetOrAdd(instruction, i => new TimeTable(i));
        }

        #region Overrides of TaskScheduler

        protected override void Init()
        {
            base.Init();
            BackgroundRunner.AddPeriodicTask(CheckSchedules, 500);
            LogEnvironment.LogDebugEvent("PeriodScheduler is now online", LogSeverity.Report);

        }

        #endregion


        /// <summary>
        /// Checks schedules
        /// </summary>
        private void CheckSchedules()
        {
            lock (pendingRequests)
            {
                DateTime? h = null;
                var requests = (from t in pendingRequests
                                where
                                    t.NextExecution == null ||
                                    t.NextExecution <= DateTime.Now
                                select new {Id = t.RequestId, Request = t, Time = t.NextExecution}).ToArray();
                foreach (var req in requests)
                {
                    pendingRequests.Remove(req.Request);
                    if (req.Time != null && req.Request.Task.Active)
                    {
                        req.Request.Task.LastExecution = DateTime.Now;
                        EnqueueTaskOnTarget(req.Request);
                    }
                    else
                    {
                        lock (req.Request.Task)
                        {
                            req.Request.Task.Active = false;
                        }
                    }
                }

            }
        }
    }
}
