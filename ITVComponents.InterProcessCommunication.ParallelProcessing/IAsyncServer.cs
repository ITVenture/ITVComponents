using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.InterProcessCommunication.ParallelProcessing
{
    /// <summary>
    /// Interface for ensuring that every object that exposes parallel functionality has the required interfaces
    /// </summary>
    public interface IAsyncServer
    {
        /// <summary>
        /// Enqueues a package into the list of processable packages
        /// </summary>
        /// <param name="package">the package that requires processing</param>
        Task<IProcessPackage> EnqueuePackage(IProcessPackage package);
    }
}
