using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.InterProcessCommunication.Shared.Helpers;
using ITVComponents.Json.Contracts;
using ITVComponents.Threading;
using ITVComponents.ParallelProcessing.Helpers;

namespace ITVComponents.ParallelProcessing
{
    public abstract class TaskBase : ITask, IManualSerializer
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
            if (asyncHelper is { IsCompleted: false })
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

        protected abstract void CompleteObjectData();

        protected virtual void ResetTask()
        {
            asyncHelper = null;
        }

        public IList<ManualSerializationData> Data { get; set; }
        public void GetObjectData()
        {
            Data.Add(ManualSerializationData.FromValue("TB##Priority", priority));
            Data.Add(ManualSerializationData.FromValue("TB##Schedules", schedules));
            Data.Add(ManualSerializationData.FromValue("TB##LastExecution", lastExecution));
            Data.Add(ManualSerializationData.FromValue("TB##Description", description));
            Data.Add(ManualSerializationData.FromValue("Success", Success));
            Data.Add(ManualSerializationData.FromValue("Error", Error));
            Data.Add(ManualSerializationData.FromValue("TB##Active", active));
            Data.Add(ManualSerializationData.FromValue("TB##ExecutingUnsafe", executingUnsafe));
            CompleteObjectData();
        }

        public virtual void ApplyObjectData()
        {
            priority = Data.GetDeserializedValue<int>("TB##Priority");
            schedules = Data.GetDeserializedValue<ICollection<SchedulerPolicy>>("TB##Schedules");
            lastExecution = Data.GetDeserializedValue<DateTime>("TB##LastExecution");
            description = Data.GetDeserializedValue<string>("TB##Description");
            Success = Data.GetDeserializedValue<bool>("Success");
            Error = Data.GetDeserializedValue<SerializedException>("Error");
            active = Data.GetDeserializedValue<bool>("TB##Active");
            executingUnsafe = Data.GetDeserializedValue<bool>("TB##ExecutingUnsafe");
        }
    }
}
