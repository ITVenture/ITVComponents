using ITVComponents.InterProcessCommunication.Shared.Helpers;
using ITVComponents.ParallelProcessing;
using ITVComponents.Threading;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.StateMachine.Parallel
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
        /// Gets remarks on this task
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Gets a value indicating whether the processing of this task has been successful
        /// </summary>
        bool Done { get; }

        /// <summary>
        /// Gets or sets a value indicating whether a specific task is active or not
        /// </summary>
        bool Active { get;}

        /// <summary>
        /// Gets a value indicating whether the Task is waiting for an external signal to be fulfilled or not. If Wait is false, the task will be fulfilled when the Processing method is completed.
        /// </summary>
        bool Wait { get; }

        /// <summary>
        /// Signals when this task is fulfilled
        /// </summary>
        /// <returns>When Fulfill is called, this task will end</returns>
        Task Processing();
    }
}
