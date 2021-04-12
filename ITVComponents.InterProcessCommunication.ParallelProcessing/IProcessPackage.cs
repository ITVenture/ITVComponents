using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ITVComponents.ParallelProcessing;

namespace ITVComponents.InterProcessCommunication.ParallelProcessing
{
    /// <summary>
    /// Basic definition of a processable package
    /// </summary>
    public interface IProcessPackage
    {
        /// <summary>
        /// Gets the Id of this process Package
        /// </summary>
        int Id { get; }

        /// <summary>
        /// The priority whith which to process this package
        /// </summary>
        int PackagePriority { get;  }

        /// <summary>
        /// Gets a text identifying the requesting system
        /// </summary>
        string RequestingSystem { get; set; }

        /// <summary>
        /// Gets the excact date and time when this package has been created
        /// </summary>
        DateTime CreationTime { get; }

        /// <summary>
        /// Gets the tasks that are provided for this Process package
        /// </summary>
        /// <returns></returns>
        IProcessTask[] GetTasks();

        /// <summary>
        /// Gets a value indicating whether this package contains tasks
        /// </summary>
        bool HasTasks { get; }

        /// <summary>
        /// Informs a client object that this Package has successfully processed
        /// </summary>
        event PackageFinishedEventHandler PackageFinished;

        /// <summary>
        /// Informs a client object that a task of this Package demands to be re-queued
        /// </summary>
        event DemandForRequeueEventHandler DemandForRequeue;
    }

    /// <summary>
    /// Eventhandler for the PackageFinished event of a ProcessPackage and the Parallel server
    /// </summary>
    /// <param name="sender">the sender of the event</param>
    /// <param name="e">the arguments for the event</param>
    public delegate void PackageFinishedEventHandler(object sender, PackageFinishedEventArgs e);

    /// <summary>
    /// EventHandler for demanding the queue to re-queue a specific item
    /// </summary>
    /// <param name="sender">the event-sender</param>
    /// <param name="target">the target items that demands to be re-queued</param>
    public delegate bool DemandForRequeueEventHandler(object sender, IProcessTask target);

    /// <summary>
    /// EventArguments that can be passed between services containing information about finished tasks
    /// </summary>
    [Serializable]
    public class PackageFinishedEventArgs:EventArgs
    {
        /// <summary>
        /// Gets or sets the tasks that were processed for this package
        /// </summary>
        public IProcessTask[] Tasks { get; set; }

        /// <summary>
        /// Gets or sets the processed package
        /// </summary>
        public IProcessPackage Package {get; set;}
    }

    /// <summary>
    /// Identifies the state of a package that requires re-integration
    /// </summary>
    public enum PackageReintegrationStatus
    {
        /// <summary>
        /// Identifies a Package that is in its initial State, which means, that its tasks have not been queued yet
        /// </summary>
        Pending,

        /// <summary>
        /// Identifies a Package that is in the processing state, which means, that its tasks have been queued into the underlaying ParallelProcessor
        /// </summary>
        Processing,

        /// <summary>
        /// Identifies a Package that has been processed entirely (with or without success)
        /// </summary>
        Done
    }

    /// <summary>
    /// Contains information about the last time an event has been triggered to indicate whether it should be triggered again
    /// </summary>
    [Serializable]
    internal class PackageFinishedEventArgsReTriggerContainer
    {
        /// <summary>
        /// Gets or sets the last Time this ReTriggerContainer was fired
        /// </summary>
        public DateTime LastTrigger { get; set; }

        /// <summary>
        /// Gets or sets the EventArgs that are suppost to be passed to the requesting client
        /// </summary>
        public PackageFinishedEventArgs Args { get; set; }
    }
}
