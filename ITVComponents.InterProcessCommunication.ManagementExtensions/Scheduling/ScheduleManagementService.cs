using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Antlr4.Runtime;
using ITVComponents.InterProcessCommunication.Shared.Security;
using ITVComponents.InterProcessCommunication.Shared.Security.PermissionBasedSecurity;
using ITVComponents.ParallelProcessing;
using ITVComponents.Plugins;

namespace ITVComponents.InterProcessCommunication.ManagementExtensions.Scheduling
{
    /// <summary>
    /// Allows a client object to manage schedulers and push schedule-requests
    /// </summary>
    [UseSecurity]
    public class ScheduleManagementService:IPlugin, ISchedulingManager
    {
        /// <summary>
        /// Gets or sets the UniqueName of this Plugin
        /// </summary>
        public string UniqueName { get; set; }

        /// <summary>
        /// Gets a list of available Schedulers
        /// </summary>
        /// <returns>a string array containing the unique names of all available scheduler objects</returns>
        public virtual SchedulerDescription[] GetAvailableSchedulers()
        {
            return (from t in TaskScheduler.GetAvailableSchedulers()
                    select
                        new SchedulerDescription
                            {
                                SchedulerName = t,
                                SupportsPushTask = TaskScheduler.GetScheduler(t).CanPushTasks
                            })
                .ToArray();
        }

        /// <summary>
        /// Gets a list of scheduled Tasks on the provided scheduler
        /// </summary>
        /// <param name="schedulerName">the scheduler on which to check for scheduled tasks</param>
        /// <returns>a list of scheduled tasks</returns>
        public virtual ScheduledTaskDescription[] GetScheduledTasks(string schedulerName)
        {
            ScheduledTaskDescription[] retVal = null;
            if (TaskScheduler.SchedulerExists(schedulerName))
            {
                TaskScheduler scheduler = TaskScheduler.GetScheduler(schedulerName);
                retVal = (from t in scheduler.GetWaitingTasks()
                          select
                              new ScheduledTaskDescription
                                  {
                                      LastExecution = t.LastExecution,
                                      Remarks = t.Remarks,
                                      RequestId = t.RequestId,
                                      TaskDescription = t.Task.Description, 
                                      MetaData = t.Task.BuildMetaData()
                                  }).ToArray();
            }

            return retVal;
        }

        /// <summary>
        /// Pushes a task to execute instantly on the given scheduler
        /// </summary>
        /// <param name="schedulerName">the scheduler on which to push the given request</param>
        /// <param name="requestId">the id of the request that needs to be executed instantly</param>
        /// <returns>a value indicating whether the request could be activated</returns>
        [HasPermission("PushSchedulerRequest")]
        public virtual bool PushRequest(string schedulerName, string requestId)
        {
            bool retVal = false;
            if (TaskScheduler.SchedulerExists(schedulerName))
            {
                TaskScheduler scheduler = TaskScheduler.GetScheduler(schedulerName);
                if (scheduler.CanPushTasks)
                {
                    retVal = scheduler.PushRequest(requestId);
                }
            }

            return retVal;
        }

        /// <summary>
        /// Führt anwendungsspezifische Aufgaben durch, die mit der Freigabe, der Zurückgabe oder dem Zurücksetzen von nicht verwalteten Ressourcen zusammenhängen.
        /// </summary>
        /// <filterpriority>2</filterpriority>
        public void Dispose()
        {
            OnDisposed();
        }

        /// <summary>
        /// Raises the disposed event
        /// </summary>
        protected virtual void OnDisposed()
        {
            EventHandler handler = Disposed;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        /// <summary>
        /// Informs a calling class of a Disposal of this Instance
        /// </summary>
        public event EventHandler Disposed;
    }
}
