using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Logging;

namespace ITVComponents.InterProcessCommunication.Shared.WatchDogs
{
    public class ServiceProcessWatchDog:ProcessWatchDog, IIpcWinSvcWatchDog
    {
        /// <summary>
        /// Registers a Service on a WatchDog instance
        /// </summary>
        /// <param name="machine">the machine on which the service is being executed</param>
        /// <param name="serviceName">the name of the WindowsService that is executed on the given application</param>
        /// <param name="processName">the name of the process</param>
        /// <param name="processId">the id of the process</param>
        /// <returns>indicates whether the healt-status was accepted by the watch-dog</returns>
        public bool RegisterService(string machine, string serviceName, string processName, int processId)
        {
            var retVal = SetProcessStatus(machine, processName, processId, false, out var processStatus);
            processStatus.MetaData(GetMetaFor(machine, serviceName));
            return retVal;
        }

        /// <summary>
        /// Registers a regular Shutdown on a previously registered service 
        /// </summary>
        /// <param name="machine">the machine on which the service is about to stop</param>
        /// <param name="serviceName">the name of the service that is stopping</param>
        /// <param name="processName">the name of the process that runs the service</param>
        /// <param name="processId">the current processId of the service</param>
        public void RegisterRegularShutdown(string machine, string serviceName, string processName, int processId)
        {
            SetProcessStatus(machine, processName, processId, false, out var processStatus);
            var meta = processStatus.MetaData<WindowsServiceMetaData>();
            if (meta != null)
            {
                meta.RegularShutdown = true;
            }
            else
            {
                LogEnvironment.LogEvent($"No Service-Metadata found for service {serviceName}...", LogSeverity.Warning);
            }
        }

        /// <summary>
        /// Enables a derived class to take further actions after a process was killed
        /// </summary>
        /// <param name="status">the process-status containing information about the killed process</param>
        protected override void ProcessKilled(ProcessStatus status)
        {
            base.ProcessKilled(status);
            var meta = status.MetaData<WindowsServiceMetaData>();
            if (meta != null)
            {
                if (!meta.RegularShutdown)
                {
                    var service = ServiceController.GetServices(meta.MachineName).First(n => n.ServiceName.Equals(meta.ServiceName, StringComparison.OrdinalIgnoreCase));
                    service.Refresh();
                    if (service.Status != ServiceControllerStatus.Stopped)
                    {
                        LogEnvironment.LogEvent($"The service {meta.ServiceName} is not in the stopped status. waiting another 5 seconds for the Servicemanager to detect the service-crash...", LogSeverity.Warning);
                        service.WaitForStatus(ServiceControllerStatus.Stopped, new TimeSpan(0, 0, 0, 5));
                        service.Refresh();
                    }

                    if (service.Status == ServiceControllerStatus.Stopped)
                    {
                        service.Start();
                        service.WaitForStatus(ServiceControllerStatus.Running);
                    }
                    else
                    {
                        LogEnvironment.LogEvent($"Unable to start Service {meta.ServiceName}. The service has the status {service.Status}", LogSeverity.Error);
                    }
                }
                else
                {
                    LogEnvironment.LogDebugEvent($"The Service {meta.ServiceName} has registered a legit shutdown. No further action required.", LogSeverity.Report);
                }
            }
            else
            {
                LogEnvironment.LogEvent($"The given process ({status.ProcessName}) contains no service-information..",LogSeverity.Warning);
            }
        }

        /// <summary>
        /// Creates the ServiceMetadata for the given Service and checks whether this service is running. If not, the service is ignored.
        /// </summary>
        /// <param name="machineName">the machine on which to check for the service</param>
        /// <param name="serviceName">the name of the service</param>
        /// <returns>a MetaData object containing information about the service</returns>
        private WindowsServiceMetaData GetMetaFor(string machineName, string serviceName)
        {
            var service = ServiceController.GetServices(machineName).First(n => n.ServiceName.Equals(serviceName, StringComparison.OrdinalIgnoreCase));
            if (service.Status == ServiceControllerStatus.Running || service.Status == ServiceControllerStatus.StartPending)
            {
                return new WindowsServiceMetaData
                {
                    DisplayName = service.DisplayName,
                    ServiceName = service.ServiceName,
                    MachineName = machineName
                };
            }

            LogEnvironment.LogEvent($"Given Service ({service.DisplayName}) is not in running-mode!", LogSeverity.Warning);
            return null;
        }
    }
}