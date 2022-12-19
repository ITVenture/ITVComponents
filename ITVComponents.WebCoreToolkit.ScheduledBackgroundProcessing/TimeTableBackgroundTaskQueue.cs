using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.ParallelProcessing.TaskSchedulers;
using ITVComponents.WebCoreToolkit.BackgroundProcessing;

namespace ITVComponents.WebCoreToolkit.ScheduledBackgroundProcessing
{
    public class TimeTableBackgroundTaskQueue:ITimeTableBackgroundTaskQueue
    {
        private List<TimeTableBackgroundTask> tasks = new List<TimeTableBackgroundTask>();

        /// <summary>
        /// Fetches the next task from this queue
        /// </summary>
        /// <param name="cancellationToken">a token that will be cancelled on service-shutdown</param>
        /// <returns>an awaitable task that will hold the next processable background-task</returns>
        public async Task<TimeTableBackgroundTask> Dequeue(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                TimeTableBackgroundTask taskForNow;
                lock (tasks)
                {
                    taskForNow = tasks.OrderBy(n => n.NextExecution).FirstOrDefault(n => n.NextExecution < DateTime.Now);
                }

                if (taskForNow != null)
                {
                    var tmpNext = taskForNow.TimeTable.GetNextExecutionTime(DateTime.Now);
                    if (tmpNext != null)
                    {
                        taskForNow.NextExecution = tmpNext.Value;
                    }
                    else
                    {
                        lock (tasks)
                        {
                            tasks.Remove(taskForNow);
                        }
                    }

                    return taskForNow.Clone();
                }

                lock (tasks)
                {
                    taskForNow = tasks.OrderBy(n => n.NextExecution).FirstOrDefault();
                }

                var now = DateTime.Now;
                var sleepSpan = taskForNow!=null && taskForNow.NextExecution > now?taskForNow.NextExecution.Subtract(DateTime.Now) : new TimeSpan(0, 0, 10, 0);
                if (sleepSpan.TotalHours > 1)
                {
                    sleepSpan = new TimeSpan(0, 1, 0, 0);
                }

                await Task.Delay(sleepSpan, cancellationToken);
            }

            return null;
        }

        /// <summary>
        /// Enqueues a parameterized task without frontend reference
        /// </summary>
        /// <param name="task">the task that needs to be processed in a background-thread</param>
        /// <param name="arguments">the arguments for the background task</param>
        /// <param name="schedule">A Timetable-parsable scheduler instruction</param>
        /// <returns>an awaitable task</returns>
        public async Task Enqueue(Func<BackgroundTaskContext, object?, Task> task, object? arguments, string schedule)
        {
            var timeTable = new TimeTable(schedule);
            var next = timeTable.GetNextExecutionTime(DateTime.Now);
            if (next != null)
            {
                await Enqueue(new TimeTableBackgroundTask
                {
                    NextExecution = next.Value,
                    AdditionalArguments = arguments,
                    Task = task,
                    TimeTable = timeTable
                });
            }
        }

        /// <summary>
        /// Enqueues a parameterless task without frontend reference
        /// </summary>
        /// <param name="task">the task that needs to be processed in a background-thread</param>
        /// <param name="schedule">A Timetable-parsable scheduler instruction</param>
        /// <returns>an awaitable task</returns>
        public async Task Enqueue(Func<BackgroundTaskContext, Task> task, string schedule)
        {
            var timeTable = new TimeTable(schedule);
            var next = timeTable.GetNextExecutionTime(DateTime.Now);
            if (next != null)
            {
                await Enqueue(new TimeTableBackgroundTask
                {
                    NextExecution = next.Value,
                    Task = (a,b)=>task(a),
                    TimeTable = timeTable
                });
            }
        }

        /// <summary>
        /// Enqueues a parameterized task with frontend reference
        /// </summary>
        /// <param name="task">the task that needs to be processed in a background-thread</param>
        /// <param name="conservedContext">the frontend context-object that provides information such as user and information about the http-request</param>
        /// <param name="arguments">the arguments for the background task</param>
        /// <param name="schedule">A Timetable-parsable scheduler instruction</param>
        /// <returns>an awaitable task</returns>
        public async Task Enqueue(Func<BackgroundTaskContext, object?, Task> task, object? conservedContext, object? arguments, string schedule)
        {
            var timeTable = new TimeTable(schedule);
            var next = timeTable.GetNextExecutionTime(DateTime.Now);
            if (next != null)
            {
                await Enqueue(new TimeTableBackgroundTask
                {
                    NextExecution = next.Value,
                    AdditionalArguments = arguments,
                    Task = task,
                    TimeTable = timeTable,
                    ConservedContext = conservedContext
                });
            }
        }

        /// <summary>
        /// Enqueues a parameterless task with frontend reference
        /// </summary>
        /// <param name="task">the task that needs to be processed in a background-thread</param>
        /// <param name="conservedContext">the frontend context-object that provides information such as user and information about the http-request</param>
        /// <param name="schedule">A Timetable-parsable scheduler instruction</param>
        /// <returns>an awaitable task</returns>
        public async Task Enqueue(Func<BackgroundTaskContext, Task> task, object? conservedContext, string schedule)
        {
            var timeTable = new TimeTable(schedule);
            var next = timeTable.GetNextExecutionTime(DateTime.Now);
            if (next != null)
            {
                await Enqueue(new TimeTableBackgroundTask
                {
                    NextExecution = next.Value,
                    Task = (a,b)=>task(a),
                    TimeTable = timeTable,
                    ConservedContext = conservedContext
                });
            }
        }

        /// <summary>
        /// Enqueues a Background-Task
        /// </summary>
        /// <param name="task">the task that needs to be processed in a background-thread</param>
        /// <returns>an awaitable task</returns>
        public Task Enqueue(TimeTableBackgroundTask task)
        {
            lock (tasks)
            {
                tasks.Add(task);
            }

            return Task.CompletedTask;
        }
    }
}
