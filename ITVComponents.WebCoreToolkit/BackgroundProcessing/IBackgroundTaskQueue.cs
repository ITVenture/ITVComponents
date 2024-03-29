﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.BackgroundProcessing
{
    /// <summary>
    /// Interface for a Background-Task queue that will serve a background worker with frontend-requests
    /// </summary>
    public interface IBackgroundTaskQueue:ITaskQueue<BackgroundTask>
    {
        /// <summary>
        /// Enqueues a parameterized task without frontend reference
        /// </summary>
        /// <param name="task">the task that needs to be processed in a background-thread</param>
        /// <param name="arguments">the arguments for the background task</param>
        /// <returns>an awaitable task</returns>
        Task Enqueue(Func<BackgroundTaskContext, object?, Task> task, object? arguments);

        /// <summary>
        /// Enqueues a parameterless task without frontend reference
        /// </summary>
        /// <param name="task">the task that needs to be processed in a background-thread</param>
        /// <returns>an awaitable task</returns>
        Task Enqueue(Func<BackgroundTaskContext, Task> task);

        /// <summary>
        /// Enqueues a parameterized task with frontend reference
        /// </summary>
        /// <param name="task">the task that needs to be processed in a background-thread</param>
        /// <param name="conservedContext">the frontend context-object that provides information such as user and information about the http-request</param>
        /// <param name="arguments">the arguments for the background task</param>
        /// <returns>an awaitable task</returns>
        Task Enqueue(Func<BackgroundTaskContext, object?, Task> task, object? conservedContext, object? arguments);

        /// <summary>
        /// Enqueues a parameterless task with frontend reference
        /// </summary>
        /// <param name="task">the task that needs to be processed in a background-thread</param>
        /// <param name="conservedContext">the frontend context-object that provides information such as user and information about the http-request</param>
        /// <returns>an awaitable task</returns>
        Task Enqueue(Func<BackgroundTaskContext, Task> task, object? conservedContext);

        /// <summary>
        /// Enqueues a Background-Task
        /// </summary>
        /// <param name="task">the task that needs to be processed in a background-thread</param>
        /// <returns>an awaitable task</returns>
        Task Enqueue(BackgroundTask task);
    }
}
