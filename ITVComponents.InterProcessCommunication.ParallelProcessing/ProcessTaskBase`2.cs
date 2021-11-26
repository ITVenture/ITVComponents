using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.InterProcessCommunication.Shared.Helpers;
using ITVComponents.ParallelProcessing;
using ITVComponents.Threading;

namespace ITVComponents.InterProcessCommunication.ParallelProcessing
{
    public abstract class ProcessTaskBase<TMe, TParent> : TaskBase, IProcessTask where TParent : ProcessPackageBase<TMe, TParent>
                                                                                where TMe : ProcessTaskBase<TMe, TParent>
    {
        private readonly int priority;

        /// <summary>
        /// Indicates whether this task is currently executing unsafe code
        /// </summary>
        [NonSerialized]
        private bool isUnsafe;

        /// <summary>
        /// Enables callers to do exclusive tasks on this object
        /// </summary>
        private object exclusiveLock = new object();

        /// <summary>
        /// Initializes a new instance of the ReleaseTask class
        /// </summary>
        /// <param name="parent">the parent item from which to get the setttings for this release</param>
        protected ProcessTaskBase(TParent parent) : this()
        {
            Parent = parent;
        }

        protected ProcessTaskBase(int priority) : this()
        {
            this.priority = priority;
        }

        private ProcessTaskBase()
        {

        }

        /// <summary>
        /// Initializes a new instance of the ReleaseTask class after deserialization
        /// </summary>
        /// <param name="info">the serialization info</param>
        /// <param name="context">the streaming context</param>
        protected ProcessTaskBase(SerializationInfo info, StreamingContext context) : base(info, context)
        {
            Done = (bool)info.GetValue("Done", typeof(bool));
            FailCount = (int)info.GetValue("FailCount", typeof(int));
            priority = (int)info.GetValue("MyPriority", typeof(int));
        }

        /// <summary>
        /// Gets the priority of this task
        /// </summary>
        public override int Priority
        {
            get { return Parent?.PackagePriority??priority; }
        }

        /// <summary>
        /// Gets a value indicating whether this job is currently executing unsafe code
        /// </summary>
        public override bool ExecutingUnsafe => isUnsafe;

        /// <summary>
        /// Gets the parent job for Settings and other information
        /// </summary>
        public TParent Parent { get; private set; }

        /// <summary>
        /// Gets a value indicating how many fails have already ocurred on this task
        /// </summary>
        public int FailCount { get; protected set; }

        /// <summary>
        /// Gets a value indicating wheter this task has finished processing (no matter whether successfully or not..)
        /// </summary>
        public bool Done { get; private set; }

        /// <summary>
        /// the serialized exception that is associated with an error that causes this item not to process
        /// </summary>
        /// <param name="ex">the thrown exception</param>
        /// <param name="recoverable">indicates whether the error that caused the fail is recoverable.</param>
        public virtual void Fail(SerializedException ex, bool recoverable)
        {
            if (!Done && (!recoverable || !(Parent?.DemandRequeue((TMe)this) ?? false)))
            {
                base.Fail(ex);
                Success = false;
                Done = true;
                Parent?.DecreaseOpenItems(false, ex);
                OnException(ex);
            }
            else
            {
                FailCount++;
            }
        }

        /// <summary>
        /// the serialized exception that is associated with an error that causes this item not to process
        /// </summary>
        /// <param name="ex">the thrown exception</param>
        public override void Fail(SerializedException ex)
        {
            Fail(ex, true);
        }

        /// <summary>
        /// Indicates on this Task that it is currently executing unsafe code
        /// </summary>
        /// <returns>a ResourceLock that resets the unsafe-flag when disposed</returns>
        public override IDisposable Unsafe()
        {
            if (isUnsafe)
            {
                throw new InvalidOperationException("Already executing unsafe code!");
            }


            isUnsafe = true;
            return new UnsafeLock(() => isUnsafe = false);
        }

        /// <summary>
        /// Demands exclusive Access for this Task
        /// </summary>
        /// <returns>a resourcelock that will be released when the exclusive access is no longer required</returns>
        public override IResourceLock DemandExclusive()
        {
            return new ResourceLock(exclusiveLock);
        }

        /// <summary>
        /// Marks the current file as done and indicates whether the run was successful
        /// </summary>
        /// <param name="success">indicates whether the run was successful</param>
        public virtual void TaskDone(bool success)
        {
            if (!Done)
            {
                Success = success;
                Done = true;
                Parent?.DecreaseOpenItems(success, null);
            }
        }

        /// <summary>
        /// Sets the Parent package of this item
        /// </summary>
        /// <param name="parent">the parent object that this task-item is part of</param>
        protected internal virtual void SetParent(TParent parent)
        {
            this.Parent = parent;
        }

        /// <summary>
        /// Allows a derived class to react on an exception
        /// </summary>
        /// <param name="ex">the exception that was thrown by a worker or scheduler</param>
        protected virtual void OnException(SerializedException ex)
        {
        }

        protected override void CompleteObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("Done", Done);
            info.AddValue("FailCount", FailCount);
            info.AddValue("MyPriority", Priority);
        }
    }
}
