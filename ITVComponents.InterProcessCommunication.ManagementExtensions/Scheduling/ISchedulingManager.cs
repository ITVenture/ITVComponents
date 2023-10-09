using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ITVComponents.Plugins;

namespace ITVComponents.InterProcessCommunication.ManagementExtensions.Scheduling
{
    /// <summary>
    /// Describes the functionality of a Scheduling manager object
    /// </summary>
    public interface ISchedulingManager
    {
        /// <summary>
        /// Gets a list of available Schedulers
        /// </summary>
        /// <returns>a string array containing the unique names of all available scheduler objects</returns>
        SchedulerDescription[] GetAvailableSchedulers();

        /// <summary>
        /// Gets a list of scheduled Tasks on the provided scheduler
        /// </summary>
        /// <param name="schedulerName">the scheduler on which to check for scheduled tasks</param>
        /// <returns>a list of scheduled tasks</returns>
        ScheduledTaskDescription[] GetScheduledTasks(string schedulerName);

        /// <summary>
        /// Pushes a task to execute instantly on the given scheduler
        /// </summary>
        /// <param name="schedulerName">the scheduler on which to push the given request</param>
        /// <param name="requestId">the id of the request that needs to be executed instantly</param>
        /// <returns>a value indicating whether the request could be activated</returns>
        bool PushRequest(string schedulerName, string requestId);
    }
}
