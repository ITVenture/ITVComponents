using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ITVComponents.InterProcessCommunication.Shared.Base;
using ITVComponents.InterProcessCommunication.Shared.Helpers;
using ITVComponents.Plugins;

namespace ITVComponents.InterProcessCommunication.ManagementExtensions.Scheduling
{
    /// <summary>
    /// Manages remote schedulers
    /// </summary>
    public class ScheduleManagementClient: ISchedulingManager
    {
        /// <summary>
        /// A connector object that is used to communicate with a foreign object
        /// </summary>
        private IBaseClient client;

        /// <summary>
        /// the proxy-object providing scheduling management functionality
        /// </summary>
        private ISchedulingManager proxy;

        /// <summary>
        /// the name of the management object
        /// </summary>
        private string managementObjectName;

        /// <summary>
        /// Initializes a new instance of the ScheduleManagementClient class
        /// </summary>
        /// <param name="client">the base client that is used to communicate with the remote service</param>
        /// <param name="managementObjectName">the name of the management object</param>
        public ScheduleManagementClient(IBaseClient client, string managementObjectName)
        {
            this.client = client;
            this.managementObjectName = managementObjectName;
            proxy = client.CreateProxy<ISchedulingManager>(managementObjectName);
        }

        /// <summary>
        /// Gets a list of available schedulers
        /// </summary>
        /// <returns>an array of names of available schedulers</returns>
        public SchedulerDescription[] GetAvailableSchedulers()
        {
            return proxy.GetAvailableSchedulers();
            /*Future<object> retVal = client.CallRemoteMethod(managementObjectName, "GetAvailableSchedulers", null);
            object obj = retVal.Result;
            if (retVal.Success)
            {
                return (SchedulerDescription[]) obj;
            }

            throw new Exception(retVal.Exception.ToString());
            //return null;*/
        }

        /// <summary>
        /// Gets a list of scheduled Tasks on the provided scheduler
        /// </summary>
        /// <param name="schedulerName">the scheduler on which to check for scheduled tasks</param>
        /// <returns>a list of scheduled tasks</returns>
        public ScheduledTaskDescription[] GetScheduledTasks(string schedulerName)
        {
            return proxy.GetScheduledTasks(schedulerName);
            /*Future<object> retVal = client.CallRemoteMethod(managementObjectName, "GetScheduledTasks",
                                                            new[] {schedulerName});
            object obj = retVal.Result;
            if (retVal.Success)
            {
                return (ScheduledTaskDescription[])obj;
            }

            return null;*/
        }

        /// <summary>
        /// Pushes a task to execute instantly on the given scheduler
        /// </summary>
        /// <param name="schedulerName">the scheduler on which to push the given request</param>
        /// <param name="requestId">the id of the request that needs to be executed instantly</param>
        /// <returns>a value indicating whether the request could be activated</returns>
        public bool PushRequest(string schedulerName, string requestId)
        {
            return proxy.PushRequest(schedulerName, requestId);
            /*Future<object> retVal = client.CallRemoteMethod(managementObjectName, "PushRequest",
                                                            new[] { schedulerName, requestId });
            object obj = retVal.Result;
            if (retVal.Success)
            {
                return (bool)obj;
            }

            return false;*/
        }
    }
}
