using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ITVComponents.InterProcessCommunication.Shared.Helpers;
using ITVComponents.ParallelProcessing;

namespace ITVComponents.InterProcessCommunication.ParallelProcessing
{
    /// <summary>
    /// Extends the Task interface for ParallelServer - relevant properties
    /// </summary>
    public interface IProcessTask:ITask
    {
        /// <summary>
        /// Gets a value indicating how many fails have ocurred on this item
        /// </summary>
        int FailCount { get; }

        /// <summary>
        /// the serialized exception that is associated with an error that causes this item not to process
        /// </summary>
        /// <param name="ex"></param>
        /// <param name="recoverable">indicates whether the error that caused the fail is recoverable.</param>
        /// <returns></returns>
        void Fail(SerializedException ex, bool recoverable);
    }
}
