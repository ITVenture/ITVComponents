using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.InterProcessCommunication.Shared.Helpers;
using ITVComponents.Threading;

namespace ITVComponents.ParallelProcessing
{
    /// <summary>
    /// Identifies a task and its priority
    /// </summary>
    public interface ITask : IDisposable
    {
        /// <summary>
        /// Gets the priority of this task
        /// </summary>
        int Priority { get; }

        /// <summary>
        /// Gets the AsyncObject that is used to implement async completion
        /// </summary>
        IAsyncResult AsyncHelper { get; internal set; }

        /*/// <summary>
        /// Gets the Default Scheduler Name that is used to schedule tasks
        /// </summary>
        string SchedulerName { get; }

        /// <summary>
        /// Gets an instruction for the scheduler how to schedule this task
        /// </summary>
        string SchedulerInstruction { get; }*/

        /// <summary>
        /// Gets configured schedules for this Task
        /// </summary>
        ICollection<SchedulerPolicy> Schedules { get; }

        /// <summary>
        /// Gets or sets the last Execution time of this Task
        /// </summary>
        DateTime LastExecution { get; set; }

        /// <summary>
        /// Gets remarks on this task
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Gets a value indicating whether the processing of this task has been successful
        /// </summary>
        bool Success { get; }

        /// <summary>
        /// Gets the serialized Error that has caused this task to fail
        /// </summary>
        SerializedException Error { get; }

        /// <summary>
        /// Gets or sets a value indicating whether a specific task is active or not
        /// </summary>
        bool Active { get; set; }

        /// <summary>
        /// Gets a value indicating whether this job is currently executing unsafe code
        /// </summary>
        bool ExecutingUnsafe { get; }

        /// <summary>
        /// the serialized exception that is associated with an error that causes this item not to process
        /// </summary>
        /// <param name="ex">the thrown exception</param>
        void Fail(SerializedException ex);

        /// <summary>
        /// Signals when this task is fulfilled
        /// </summary>
        /// <returns>When Fulfill is called, this task will end</returns>
        Task Processing();

        /// <summary>
        /// Is automatically called after the task has ended
        /// </summary>
        void Fulfill();

        /// <summary>
        /// Demands exclusive Access for this Task
        /// </summary>
        /// <returns>a resourcelock that will be released when the exclusive access is no longer required</returns>
        IResourceLock DemandExclusive();

        /// <summary>
        /// Indicates whether this job Task is a duplicate of an other job
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        bool IsDuplicateOf(ITask other);

        /// <summary>
        /// Creates Meta-Data - Informations that can be used to identify a specific Task
        /// </summary>
        /// <returns>a Dictionary that is uniquely identifying a specific Task</returns>
        Dictionary<string, object> BuildMetaData();

        /// <summary>
        /// Indicates on this Task that it is currently executing unsafe code
        /// </summary>
        /// <returns>a ResourceLock that resets the unsafe-flag when disposed</returns>
        IDisposable Unsafe();
    }
}