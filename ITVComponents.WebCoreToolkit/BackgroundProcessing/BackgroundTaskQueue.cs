using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.BackgroundProcessing
{
    /// <summary>
    /// A Task-Queue that will serve the Background-Taskprocessor class with frontend requests
    /// </summary>
    internal class BackgroundTaskQueue : IBackgroundTaskQueue
    {
        /// <summary>
        /// A Channel that will receive background-tasks from the frontend-threads
        /// </summary>
        private Channel<BackgroundTask> tasks;

        /// <summary>
        /// Initializes a new instance of the BackgroundTaskQueue class with the given capacity
        /// </summary>
        /// <param name="capacity">the capacity of the background-task queue</param>
        public BackgroundTaskQueue(int capacity)
        {
            tasks = Channel.CreateBounded<BackgroundTask>(new BoundedChannelOptions(capacity)
            {
                FullMode = BoundedChannelFullMode.Wait
            });
        }


        /// <summary>
        /// Enqueues a parameterized task without frontend reference
        /// </summary>
        /// <param name="task">the task that needs to be processed in a background-thread</param>
        /// <param name="arguments">the arguments for the background task</param>
        /// <returns>an awaitable task</returns>
        public async Task Enqueue(Func<BackgroundTaskContext, object?, Task> task, object? arguments)
        {
            await Enqueue(new BackgroundTask
            {
                Task = task,
                AdditionalArguments = arguments
            });
        }

        /// <summary>
        /// Enqueues a parameterless task without frontend reference
        /// </summary>
        /// <param name="task">the task that needs to be processed in a background-thread</param>
        /// <returns>an awaitable task</returns>
        public async Task Enqueue(Func<BackgroundTaskContext, Task> task)
        {
            await Enqueue(new BackgroundTask
            {
                Task = async (c, a) => await task(c),
                AdditionalArguments = null
            });
        }

        /// <summary>
        /// Enqueues a parameterized task with frontend reference
        /// </summary>
        /// <param name="task">the task that needs to be processed in a background-thread</param>
        /// <param name="conservedContext">the frontend context-object that provides information such as user and information about the http-request</param>
        /// <param name="arguments">the arguments for the background task</param>
        /// <returns>an awaitable task</returns>
        public async Task Enqueue(Func<BackgroundTaskContext, object?, Task> task, object? conservedContext, object? arguments)
        {
            await Enqueue(new BackgroundTask
            {
                Task = task,
                AdditionalArguments = arguments,
                ConservedContext = conservedContext
            });
        }

        /// <summary>
        /// Enqueues a parameterless task with frontend reference
        /// </summary>
        /// <param name="task">the task that needs to be processed in a background-thread</param>
        /// <param name="conservedContext">the frontend context-object that provides information such as user and information about the http-request</param>
        /// <returns>an awaitable task</returns>
        public async Task Enqueue(Func<BackgroundTaskContext, Task> task, object? conservedContext)
        {
            await Enqueue(new BackgroundTask
            {
                Task = async (c, a) => await task(c),
                AdditionalArguments = null,
                ConservedContext = conservedContext
            });
        }

        /// <summary>
        /// Enqueues a Background-Task
        /// </summary>
        /// <param name="task">the task that needs to be processed in a background-thread</param>
        /// <returns>an awaitable task</returns>
        public async Task Enqueue(BackgroundTask task)
        {
            if (task == null)
            {
                throw new ArgumentNullException(nameof(task));
            }

            await tasks.Writer.WriteAsync(task);
        }

        /// <summary>
        /// Fetches the next task from this queue
        /// </summary>
        /// <param name="cancellationToken">a token that will be cancelled on service-shutdown</param>
        /// <returns>an awaitable task that will hold the next processable background-task</returns>
        public async Task<BackgroundTask> Dequeue(CancellationToken cancellationToken)
        {
            return await tasks.Reader.ReadAsync(cancellationToken);
        }
    }
}
