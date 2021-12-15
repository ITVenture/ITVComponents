using System;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.BackgroundProcessing;

namespace ITVComponents.WebCoreToolkit.ScheduledBackgroundProcessing
{
    /// <summary>
    /// Interface for a time-table based task queue
    /// </summary>
    public interface ITimeTableBackgroundTaskQueue:ITaskQueue<TimeTableBackgroundTask>
    {
        /// <summary>
        /// Enqueues a parameterized task without frontend reference
        /// </summary>
        /// <param name="task">the task that needs to be processed in a background-thread</param>
        /// <param name="arguments">the arguments for the background task</param>
        /// <param name="schedule">A Timetable-parsable scheduler instruction</param>
        /// <returns>an awaitable task</returns>
        Task Enqueue(Func<BackgroundTaskContext, object?, Task> task, object? arguments, string schedule);

        /// <summary>
        /// Enqueues a parameterless task without frontend reference
        /// </summary>
        /// <param name="task">the task that needs to be processed in a background-thread</param>
        /// <param name="schedule">A Timetable-parsable scheduler instruction</param>
        /// <returns>an awaitable task</returns>
        Task Enqueue(Func<BackgroundTaskContext, Task> task, string schedule);

        /// <summary>
        /// Enqueues a parameterized task with frontend reference
        /// </summary>
        /// <param name="task">the task that needs to be processed in a background-thread</param>
        /// <param name="conservedContext">the frontend context-object that provides information such as user and information about the http-request</param>
        /// <param name="arguments">the arguments for the background task</param>
        /// <param name="schedule">A Timetable-parsable scheduler instruction</param>
        /// <returns>an awaitable task</returns>
        Task Enqueue(Func<BackgroundTaskContext, object?, Task> task, object? conservedContext, object? arguments, string schedule);

        /// <summary>
        /// Enqueues a parameterless task with frontend reference
        /// </summary>
        /// <param name="task">the task that needs to be processed in a background-thread</param>
        /// <param name="conservedContext">the frontend context-object that provides information such as user and information about the http-request</param>
        /// <param name="schedule">A Timetable-parsable scheduler instruction</param>
        /// <returns>an awaitable task</returns>
        Task Enqueue(Func<BackgroundTaskContext, Task> task, object? conservedContext, string schedule);

        /// <summary>
        /// Enqueues a Background-Task
        /// </summary>
        /// <param name="task">the task that needs to be processed in a background-thread</param>
        /// <returns>an awaitable task</returns>
        Task Enqueue(TimeTableBackgroundTask task);
    }
}
