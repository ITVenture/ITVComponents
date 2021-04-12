using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.InterProcessCommunication.Shared.WatchDogs
{
    public interface IIpcWinSvcWatchDog:IIpcWatchDog
    {
        /// <summary>
        /// Registers a Service on a WatchDog instance
        /// </summary>
        /// <param name="machine">the machine on which the service is being executed</param>
        /// <param name="serviceName">the name of the WindowsService that is executed on the given application</param>
        /// <param name="processName">the name of the process</param>
        /// <param name="processId">the id of the process</param>
        /// <returns>indicates whether the healt-status was accepted by the watch-dog</returns>
        bool RegisterService(string machine, string serviceName, string processName, int processId);

        /// <summary>
        /// Registers a regular Shutdown on a previously registered service 
        /// </summary>
        /// <param name="machine">the machine on which the service is about to stop</param>
        /// <param name="serviceName">the name of the service that is stopping</param>
        /// <param name="processName">the name of the process that runs the service</param>
        /// <param name="processId">the current processId of the service</param>
        void RegisterRegularShutdown(string machine, string serviceName, string processName, int processId);
    }
}
