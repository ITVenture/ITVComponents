using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.BackgroundProcessing
{
    /// <summary>
    /// General Task queue for a background service
    /// </summary>
    public interface ITaskQueue<TBackgroundTask> where TBackgroundTask:BackgroundTask
    {
        /// <summary>
        /// Fetches the next task from this queue
        /// </summary>
        /// <param name="cancellationToken">a token that will be cancelled on service-shutdown</param>
        /// <returns>an awaitable task that will hold the next processable background-task</returns>
        Task<TBackgroundTask> Dequeue(CancellationToken cancellationToken);
    }
}
