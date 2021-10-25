using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.InterProcessCommunication.Shared.Helpers;
using ITVComponents.Threading;
using ITVComponents.ParallelProcessing.Helpers;

namespace ITVComponents.ParallelProcessing
{
    public abstract class TaskBase : ITask, ISerializable
    {
        private IAsyncResult asyncHelper;
        private int priority;
        private ICollection<SchedulerPolicy> schedules;
        private DateTime lastExecution;
        private string description;
        private bool active;
        private bool executingUnsafe;

        protected TaskBase()
        {
        }

        protected TaskBase(SerializationInfo info, StreamingContext context) : this()
        {
            priority = (int)info.GetValue("TB##Priority", typeof(int));
            schedules = (ICollection<SchedulerPolicy>)info.GetValue("TB##Schedules", typeof(ICollection<SchedulerPolicy>));
            lastExecution = (DateTime)info.GetValue("TB##LastExecution", typeof(DateTime));
            description = (string)info.GetValue("TB##Description", typeof(string));
            Success = (bool)info.GetValue("Success", typeof(bool));
            Error = (SerializedException)info.GetValue("Error", typeof(SerializedException));
            active = (bool)info.GetValue("TB##Active", typeof(bool));
            executingUnsafe = (bool)info.GetValue("TB##ExecutingUnsafe", typeof(bool));
        }

        /// <summary>Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.</summary>
        public virtual void Dispose()
        {
        }

        /// <summary>
        /// Gets the priority of this task
        /// </summary>
        public virtual int Priority
        {
            get => priority;
            protected set => priority = value;
        }

        IAsyncResult ITask.AsyncHelper
        {
            get => asyncHelper;
            set => asyncHelper = value;
        }

        /// <summary>
        /// Gets configured schedules for this Task
        /// </summary>
        public virtual ICollection<SchedulerPolicy> Schedules
        {
            get => schedules;
            protected set => schedules = value;
        }

        /// <summary>
        /// Gets or sets the last Execution time of this Task
        /// </summary>
        public virtual DateTime LastExecution
        {
            get => lastExecution;
            set => lastExecution = value;
        }

        /// <summary>
        /// Gets remarks on this task
        /// </summary>
        public virtual string Description
        {
            get => description;
            protected set => description = value;
        }

        /// <summary>
        /// Gets a value indicating whether the processing of this task has been successful
        /// </summary>
        public bool Success { get; protected set; }

        /// <summary>
        /// Gets the serialized Error that has caused this task to fail
        /// </summary>
        public SerializedException Error { get; protected set; }

        /// <summary>
        /// Gets or sets a value indicating whether a specific task is active or not
        /// </summary>
        public virtual bool Active
        {
            get => active;
            set => active = value;
        }

        /// <summary>
        /// Gets a value indicating whether this job is currently executing unsafe code
        /// </summary>
        public virtual bool ExecutingUnsafe
        {
            get => executingUnsafe;
            protected set => executingUnsafe = value;
        }

        /// <summary>
        /// the serialized exception that is associated with an error that causes this item not to process
        /// </summary>
        /// <param name="ex">the thrown exception</param>
        public virtual void Fail(SerializedException ex)
        {
            this.Error = ex;
            Success = false;
            if (asyncHelper != null)
            {
                AsyncHelper.Fulfill(asyncHelper);
            }
        }

        /// <summary>
        /// Signals when this task is fulfilled
        /// </summary>
        /// <returns>When Fulfill is called, this task will end</returns>
        public virtual Task Processing()
        {
            return AsyncHelper.BeginAsync(this);
        }

        /// <summary>
        /// Is automatically called after the task has ended
        /// </summary>
        public virtual void Fulfill()
        {
            if (asyncHelper != null)
            {
                AsyncHelper.Fulfill(asyncHelper);
            }
        }

        /// <summary>
        /// Demands exclusive Access for this Task
        /// </summary>
        /// <returns>a resourcelock that will be released when the exclusive access is no longer required</returns>
        public abstract IResourceLock DemandExclusive();

        /// <summary>
        /// Indicates whether this job Task is a duplicate of an other job
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public abstract bool IsDuplicateOf(ITask other);

        /// <summary>
        /// Creates Meta-Data - Informations that can be used to identify a specific Task
        /// </summary>
        /// <returns>a Dictionary that is uniquely identifying a specific Task</returns>
        public abstract Dictionary<string, object> BuildMetaData();

        /// <summary>
        /// Indicates on this Task that it is currently executing unsafe code
        /// </summary>
        /// <returns>a ResourceLock that resets the unsafe-flag when disposed</returns>
        public abstract IDisposable Unsafe();

        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("TB##Priority", priority);
            info.AddValue("TB##Schedules", schedules);
            info.AddValue("TB##LastExecution", lastExecution);
            info.AddValue("TB##Description", description);
            info.AddValue("Success", Success);
            info.AddValue("Error", Error);
            info.AddValue("TB##Active", active);
            info.AddValue("TB##ExecutingUnsafe", executingUnsafe);
            CompleteObjectData(info, context);
        }

        protected abstract void CompleteObjectData(SerializationInfo info, StreamingContext context);

        protected virtual void ResetTask()
        {
            asyncHelper = null;
        }
    }
}
