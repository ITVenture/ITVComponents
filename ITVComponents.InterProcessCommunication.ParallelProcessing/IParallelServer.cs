using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ITVComponents.InterProcessCommunication.ParallelProcessing
{
    /// <summary>
    /// Interface for ensuring that every object that exposes parallel functionality has the required interfaces
    /// </summary>
    public interface IParallelServer
    {
        /// <summary>
        /// Enables a client to commit that it recieved the TaskDone event for a specific package
        /// </summary>
        /// <param name="requestingSystem">the identifier of the requesting system</param>
        /// <param name="packageId">the package identifier for that system</param>
        void CommitTaskDoneRecieved(string requestingSystem, int packageId);

        /// <summary>
        /// Enqueues a package into the list of processable packages
        /// </summary>
        /// <param name="package">the package that requires processing</param>
        void EnqueuePackage(IProcessPackage package);

        /// <summary>
        /// Informs listening clients that a package that has been passed for processing is done
        /// </summary>
        event PackageFinishedEventHandler PackageProcessed;
    }
}
