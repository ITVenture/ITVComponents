using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.Logging;
using ITVComponents.Plugins;
using ITVComponents.Serialization;
using Microsoft.Win32;

namespace ITVComponents.ParallelProcessing
{
    /// <summary>
    /// Unspecific implementation for a TaskScheduler. Tasks are being added to the processor immediately
    /// </summary>
    public class TaskScheduler : IDeferredInit, IDisposable
    {
        private readonly string uniqueName;

        /// <summary>
        /// Holds a list of available schedulers
        /// </summary>
        private static ConcurrentDictionary<string, TaskScheduler> availableSchedulers;

        /// <summary>
        /// AutoResetEvent that is used to synchronize background processes and manual push actions
        /// </summary>
        private AutoResetEvent synchDoor = new AutoResetEvent(true);

        /// <summary>
        /// Initializes static members of the TaskScheduler class
        /// </summary>
        static TaskScheduler()
        {
            availableSchedulers = new ConcurrentDictionary<string, TaskScheduler>();
        }

        /// <summary>
        /// Prevents a default instance of the TaskScheduler class from being created
        /// </summary>
        public TaskScheduler(string uniqueName)
        {
            this.uniqueName = uniqueName;
        }

        /// <summary>
        /// Indicates whether this deferrable init-object is already initialized
        /// </summary>
        public bool Initialized { get; private set; }

        /// <summary>
        /// Gets the name of this scheduler
        /// </summary>
        public string SchedulerName => uniqueName;

        /// <summary>
        /// Indicates whether this Object requires immediate Initialization right after calling the constructor
        /// </summary>
        public bool ForceImmediateInitialization => false;

        /// <summary>
        /// Indicates whether a derived Scheduler supports pushing requests to run immediate
        /// </summary>
        public virtual bool CanPushTasks
        {
            get { return false; }
        }

        /// <summary>
        /// Gets a value indicating whether a specific Scheduler is currently available
        /// </summary>
        /// <param name="schedulerName">the scheduler name that is requested by a task</param>
        /// <returns>the required scheduler name</returns>
        public static bool SchedulerExists(string schedulerName)
        {
            return availableSchedulers.ContainsKey(schedulerName);
        }

        /// <summary>
        /// Gets the scheduler with the given name
        /// </summary>
        /// <param name="schedulerName">the scheduler name for which to get the appropriate scheduler instance</param>
        /// <returns>a scheduler instance with the given name</returns>
        public static TaskScheduler GetScheduler(string schedulerName)
        {
            return availableSchedulers[schedulerName];
        }

        /// <summary>
        /// Initializes this deferred initializable object
        /// </summary>
        public void Initialize()
        {
            if (!Initialized)
            {
                try
                {
                    availableSchedulers.AddOrUpdate(uniqueName, (s) => this, (s, o) => this);
                    Init();
                }
                finally
                {
                    Initialized = true;
                }
            }
        }

        /// <summary>
        /// Schedules a task for execution
        /// </summary>
        /// <param name="task">the task that needs to be executed</param>
        /// <returns>a value indicating whether the task was scheduled</returns>
        public virtual bool ScheduleTask(ScheduleRequest task)
        {
            EnqueueTaskOnTarget(task);
            return true;
        }

        /// <summary>
        /// Gets a list of available Schedule - Requests on this scheduler
        /// </summary>
        /// <returns>an array of tasks that are waiting to be scheduled</returns>
        public virtual ScheduleRequest[] GetWaitingTasks()
        {
            return null;
        }

        /// <summary>
        /// Executes a task that was previously scheduled if appropriate
        /// </summary>
        /// <param name="task">the task that was requested for execution</param>
        /// <param name="action">the action that is performed to the provided task</param>
        public virtual bool RunScheduledTask(ScheduleRequest task, Action<ITask> action)
        {
            action(task.Task);
            return true;
        }

        /// <summary>
        /// Executes a task that was previously scheduled if appropriate
        /// </summary>
        /// <param name="task">the task that was requested for execution</param>
        /// <param name="action">the action that is performed to the provided task</param>
        public virtual async Task<bool> RunScheduledTaskAsync(ScheduleRequest task, Func<ITask, Task> action)
        {
            await action(task.Task);
            return true;
        }

        /// <summary>
        /// Creates a Task request for this TaskScheduler with the Task that needs to be executed and the queue that will execute it
        /// </summary>
        /// <param name="parallelTaskProcessor">the TaskProcessor that will run the provided task later</param>
        /// <param name="task">the task that needs to be executed</param>
        /// <returns>a scheduler - request for the given processor and task</returns>
        public virtual ScheduleRequest CreateRequest(ParallelTaskProcessor parallelTaskProcessor, ITask task)
        {
            return new ScheduleRequest(uniqueName, parallelTaskProcessor, task);
        }

        /// <summary>
        /// On a derived class, this Method can push an SchedulingRequest, so it will be executed immediately
        /// </summary>
        /// <param name="requestId">the unique id of the request that requires immediate execution</param>
        /// <returns>a value indicating whether the request was found and pushing was successful</returns>
        public virtual bool PushRequest(string requestId)
        {
            return false;
        }

        /// <summary>
        /// Gets a list of available Scheduler objects
        /// </summary>
        /// <returns>a string containing all available Schedulers</returns>
        public static string[] GetAvailableSchedulers()
        {
            return availableSchedulers.Keys.ToArray();
        }

        public virtual void Dispose()
        {
        }

        /// <summary>
        /// Locks the scheduler for special tasks
        /// </summary>
        /// <param name="action"></param>
        protected void LockForTask(Action action)
        {
            synchDoor.WaitOne();
            try
            {
                action();
            }
            finally
            {
                synchDoor.Set();
            }
        }

        /// <summary>
        /// Runs Initializations on derived objects
        /// </summary>
        protected virtual void Init()
        {
        }

        /// <summary>
        /// Enqueues a task on the target taskprocessor object
        /// </summary>
        /// <param name="task">the task selected for execution</param>
        protected void EnqueueTaskOnTarget(ScheduleRequest task)
        {
            task.Target.TaskScheduled(new TaskContainer {Request = task, Task = task.Task});
        }

        /// <summary>
        /// A Request for Scheduling
        /// </summary>
        [Serializable]
        public class ScheduleRequest:ISerializable
        {
            /// <summary>
            /// sha object used to calculate task request ids
            /// </summary>
            private static SHA256 sha = new SHA256Managed();

            /// <summary>
            /// holds a list of instructions for the current request
            /// </summary>
            private List<string> instructions = new List<string>();

            /// <summary>
            /// Initializes a new instance of the ScheduleRequest class
            /// </summary>
            /// <param name="schedulerName">the name of the responsible Scheduler for this request</param>
            /// <param name="targetProcessor">the target processor that is used to run this request</param>
            /// <param name="task">the task to run on the given processor</param>
            /// <param name="instruction">the default-instruction for this request</param>
            /// <param name="lastExecution">the last time, the underlaying task was executed</param>
            public ScheduleRequest(string schedulerName, ParallelTaskProcessor targetProcessor, ITask task,
                                   DateTime? lastExecution = null)
            {
                SchedulerName = schedulerName;
                Target = targetProcessor;
                Task = task;
                LastExecution = lastExecution ?? DateTime.MinValue;
                StringBuilder metaInfo = new StringBuilder();
                var metaData = task.BuildMetaData();
                if (metaData != null)
                {
                    foreach (KeyValuePair<string, object> metaRecord in metaData)
                    {
                        metaInfo.AppendFormat(":{0}:{1}", metaRecord.Key, metaRecord.Value);
                    }
                }
                LogEnvironment.LogDebugEvent($"Creating Schedule-Requeset for the following Meta-Data: {metaInfo}",
                    LogSeverity.Report);
                RequestId =
                    BitConverter.ToString(
                        sha.ComputeHash(
                            Encoding.UTF32.GetBytes(
                                string.Format("{0:yyyyMMddHHmmssffff}:{1}:{2}:{3:yyyyMMddHHmmssffff}{4}", DateTime.Now,
                                              targetProcessor.GetHashCode(), task.Description, LastExecution,metaInfo))));
            }

            /// <summary>
            /// Initializes a ScheduleRequest from its serialization representation
            /// </summary>
            /// <param name="info">the serialization info containing the objects that have been serialized</param>
            /// <param name="context">the current streaming context</param>
            public ScheduleRequest(SerializationInfo info, StreamingContext context)
            {
                Target = ParallelTaskProcessor.GetInstance(info.GetString("TargetName"));
                Task = info.GetValue("Task",typeof(ITask)) as ITask;
                LastExecution = info.GetDateTime("LastExecution");
                instructions = new List<string>((string[]) info.GetValue("Instructions", typeof (string[])));
                Remarks = info.GetString("Remarks");
                RequestId = info.GetString("RequestId");
                SchedulerName = info.GetString("SchedulerName");
            }

            /// <summary>
            /// Gets or sets the name of the scheduelr that is responsible for scheduling this
            /// </summary>
            public string SchedulerName { get; private set; }

            /// <summary>
            /// Gets the ParallelTaskProcessor on which to enqueue the Task in this request
            /// </summary>
            public ParallelTaskProcessor Target { get; private set; }

            /// <summary>
            /// Gets the TargetTask to enqueue
            /// </summary>
            public ITask Task { get; private set; }

            /// <summary>
            /// Gets or sets the last time this Request was executed
            /// </summary>
            public DateTime LastExecution { get; private set; }

            /// <summary>
            /// Gets or sets remarks on this Request
            /// </summary>
            public string Remarks { get; set; }

            /// <summary>
            /// Gets the unique RequestId of this Request
            /// </summary>
            public string RequestId { get; private set; }

            /// <summary>
            /// Gets all instructions for this task scheduler
            /// </summary>
            public virtual IEnumerable<string> Instructions { get { return instructions; } }

            /// <summary>
            /// Adds an instruction to the list of instructions for this schedule request
            /// </summary>
            /// <param name="instruction">the instruction to be executed when checking this request</param>
            public void AddInstruction(string instruction)
            {
                AddInstruction(instruction, true);
            }

            /// <summary>
            /// Füllt eine <see cref="T:System.Runtime.Serialization.SerializationInfo"/> mit den Daten, die zum Serialisieren des Zielobjekts erforderlich sind.
            /// </summary>
            /// <param name="info">Die mit Daten zu füllende <see cref="T:System.Runtime.Serialization.SerializationInfo"/>. </param><param name="context">Das Ziel (siehe <see cref="T:System.Runtime.Serialization.StreamingContext"/>) dieser Serialisierung. </param><exception cref="T:System.Security.SecurityException">Der Aufrufer verfügt nicht über die erforderliche Berechtigung. </exception>
            public virtual void GetObjectData(SerializationInfo info, StreamingContext context)
            {
                info.AddValue("TargetName", Target.Identifier);
                info.AddValue("Task", Task);
                info.AddValue("LastExecution", LastExecution);
                info.AddValue("Instructions", instructions.ToArray());
                info.AddValue("Remarks", Remarks);
                info.AddValue("RequestId", RequestId);
                info.AddValue("SchedulerName",SchedulerName);
            }

            /// <summary>
            /// Adds the instruction to the list of schedule-instructions and sets the remarks if requested
            /// </summary>
            /// <param name="instruction">the instruction that is used for this schedule-request</param>
            /// <param name="setRemarks">indicates whether to use the default-remarks</param>
            protected virtual void AddInstruction(string instruction, bool setRemarks)
            {
                instructions.Add(instruction);
                if (setRemarks)
                {
                    Remarks = string.Format("Execution-Condition: {0}",
                        string.Join(" OR ", instructions));
                }
            }
        }
    }
}
