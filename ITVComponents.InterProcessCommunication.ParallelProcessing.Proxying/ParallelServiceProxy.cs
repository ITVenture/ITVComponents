using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.DataAccess.Extensions;
using ITVComponents.InterProcessCommunication.Shared.Base;
using ITVComponents.InterProcessCommunication.Shared.Helpers;
using ITVComponents.Logging;
using ITVComponents.Plugins;
using ITVComponents.Serialization;
using ITVComponents.Threading;

namespace ITVComponents.InterProcessCommunication.ParallelProcessing.Proxying
{
    /// <summary>
    /// Adds proxy functionality to the ParallelService environment. Use this class if you have a service that requires reboots in specific situations
    /// </summary>
    public abstract class ParallelServiceProxy:IParallelServer, IStatusSerializable, IPlugin
    {
        /// <summary>
        /// the ProxyConsumer object that is capable for connections to the real worker of this proxy
        /// </summary>
        private IBidirectionalClient client;

        /// <summary>
        /// the name of the remote object that does the effective work
        /// </summary>
        private string remoteObjectName;

        /// <summary>
        /// the link to the effective service link
        /// </summary>
        private ServiceController serviceLink;

        /// <summary>
        /// the name of the service that exposes the connection used by this ParallelProxy object
        /// </summary>
        private string serviceName;

        /// <summary>
        /// contains a list of all packages that have been passed to the worker service
        /// </summary>
        private List<PackageSender> processingPackages;

        /// <summary>
        /// contains a list of all packages that have been committed done by the service but not committed back by the calling service
        /// </summary>
        private List<PackageTrigger> unCommittedPackages;

        /// <summary>
        /// indicates whether a reboot of the target service is required
        /// </summary>
        private bool requireReboot = false;

        private DateTime timeOfCommunicationLoss = DateTime.MinValue;

        /// <summary>
        /// Initializes a new instance of the ParallelServiceProxy class
        /// </summary>
        /// <param name="client">the proxy client that is used for the interprocess communication</param>
        /// <param name="remoteObjectName">the name of the remote object providing the required functionality</param>
        /// <param name="serviceName">the servicename that needs to be restarted on errors</param>
        protected ParallelServiceProxy(IBidirectionalClient client, string remoteObjectName, string serviceName)
            : this()
        {
            this.client = client;
            this.remoteObjectName = remoteObjectName;
            this.serviceName = serviceName;
        }

        /// <summary>
        /// Prevents a default instance of the ParallelServiceProxy class from being created
        /// </summary>
        private ParallelServiceProxy()
        {
            processingPackages = new List<PackageSender>();
            unCommittedPackages = new List<PackageTrigger>();
        }

        /// <summary>
        /// Gets or sets the UniqueName of this Plugin
        /// </summary>
        public string UniqueName { get; set; }

        /// <summary>
        /// Enables a client to commit that it recieved the TaskDone event for a specific package
        /// </summary>
        /// <param name="requestingSystem">the identifier of the requesting system</param>
        /// <param name="packageId">the package identifier for that system</param>
        public void CommitTaskDoneRecieved(string requestingSystem, int packageId)
        {
            lock (unCommittedPackages)
            {
                PackageTrigger psc =
                    (from t in unCommittedPackages
                     where t.Args.Package.RequestingSystem == requestingSystem && t.Args.Package.Id == packageId
                     select t).FirstOrDefault();
                if (psc != null)
                {
                    unCommittedPackages.Remove(psc);
                    client.CallRemoteMethod(remoteObjectName, "CommitTaskDoneRecieved",
                                                                 new object[] {requestingSystem, packageId});
                }
            }
        }

        /// <summary>
        /// Enqueues a package into the list of processable packages
        /// </summary>
        /// <param name="package">the package that requires processing</param>
        public void EnqueuePackage(IProcessPackage package)
        {
            lock (processingPackages)
            {
                processingPackages.Add(new PackageSender {Package = package, Sent = false});
            }
        }

        /// <summary>
        /// Führt anwendungsspezifische Aufgaben durch, die mit der Freigabe, der Zurückgabe oder dem Zurücksetzen von nicht verwalteten Ressourcen zusammenhängen.
        /// </summary>
        /// <filterpriority>2</filterpriority>
        public virtual void Dispose()
        {
            BackgroundRunner.RemovePeriodicTask(ReTriggerPendingTasks);
            OnDisposed();
        }

        /// <summary>
        /// Gets the Runtime information required to restore the status when the application restarts
        /// </summary>
        /// <returns>an object serializer containing all required data for object re-construction on application reboot</returns>
        public RuntimeInformation GetPostDisposalSerializableStaus()
        {
            return GetRuntimeStatus();
        }

        /// <summary>
        /// Applies Runtime information that was loaded from a file
        /// </summary>
        /// <param name="runtimeInformation">the runtime information describing the status of this object before the last shutdown</param>
        public void LoadRuntimeStatus(RuntimeInformation runtimeInformation)
        {
            Initialize(runtimeInformation);
        }

        /// <summary>
        /// Allows this object to do required initializations when no runtime status is provided by the calling object
        /// </summary>
        public void InitializeWithoutRuntimeInformation()
        {
            Initialize(null);
        }

        /// <summary>
        /// Is called when the runtime is completly available and ready to run
        /// </summary>
        public void RuntimeReady()
        {
            StartupComplete();
        }

        /// <summary>
        /// Initializes this service
        /// </summary>
        /// <param name="runtimeInformation">the optional runtime information that was used to initialize the service</param>
        protected virtual void Initialize(RuntimeInformation runtimeInformation)
        {
            if (runtimeInformation != null)
            {
                processingPackages.AddRange(runtimeInformation["openPackages"] as PackageSender[]);
                unCommittedPackages.AddRange(runtimeInformation["unCommittedPackages"] as PackageTrigger[]);
            }
        }

        /// <summary>
        /// Gets the runtime status for this Proxy service
        /// </summary>
        /// <returns>a Runtime information conatining all open jobs</returns>
        protected virtual RuntimeInformation GetRuntimeStatus()
        {
            RuntimeInformation retVal = new RuntimeInformation();
            retVal.Add("openPackages", processingPackages.ToArray());
            retVal.Add("unCommittedPackages", unCommittedPackages.ToArray());
            return retVal;
        }

        /// <summary>
        /// Raises the PackageProcessed event
        /// </summary>
        /// <param name="e">the argument received from the real service</param>
        protected virtual void OnPackageProcessed(PackageFinishedEventArgs e)
        {
            PackageFinishedEventHandler handler = PackageProcessed;
            if (handler != null) handler(this, e);
        }

        /// <summary>
        /// Raises the Disposed event
        /// </summary>
        protected virtual void OnDisposed()
        {
            EventHandler handler = Disposed;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        /// <summary>
        /// Reboots the worker service
        /// </summary>
        protected void RebootService()
        {
            bool success = false;
            while (!success)
            {
                KillService();
                try
                {
                    serviceLink.Start();
                    serviceLink.WaitForStatus(ServiceControllerStatus.Running);
                    success = true;
                }
                catch (Exception ex)
                {
                    LogEnvironment.LogDebugEvent(ex.Message, LogSeverity.Error);
                    serviceLink.Dispose();
                    InitializeService(false);
                }
            }
        }

        /// <summary>
        /// Indicates for a specific ProcessTask whether the state of this package may require the target service to reboot
        /// </summary>
        /// <param name="processTask">the processed task object that was returned from the worker service</param>
        /// <returns></returns>
        protected abstract bool RequireReboot(IProcessTask processTask);

        /// <summary>
        /// Indicates whether the given task was successful
        /// </summary>
        /// <param name="task">the task to Check</param>
        /// <returns>a value indicating whether the processTask can be considered successful</returns>
        protected virtual bool Success(IProcessTask task)
        {
            return task.Success;
        }

        /// <summary>
        /// Indicates whether the given package was successful
        /// </summary>
        /// <param name="package">the package to Check</param>
        /// <returns>a value indicating whether the package can be considered successful</returns>
        protected virtual bool Success(IProcessPackage package)
        {
            return true;
        }

        /// <summary>
        /// Checks the Windows Service environment for the desired service name and opens a handle to it
        /// </summary>
        /// <param name="startImmediate">indicates whether to immediately start the service after linking</param>
        private void InitializeService(bool startImmediate=true)
        {
            ServiceController[] services = ServiceController.GetServices();
            serviceLink = (from t in services where t.ServiceName.Equals(serviceName, StringComparison.OrdinalIgnoreCase) select t).FirstOrDefault();
            if (serviceLink == null)
            {
                throw new Exception("No such Service was fund!");
            }

            if (startImmediate)
            {
                if (serviceLink.Status != ServiceControllerStatus.Running &&
                    serviceLink.Status != ServiceControllerStatus.StartPending)
                {
                    serviceLink.Start();
                }

                serviceLink.WaitForStatus(ServiceControllerStatus.Running);
            }
        }

        /// <summary>
        /// Completes the satrtup and puts this proxy into an operational state
        /// </summary>
        private void StartupComplete()
        {
            client.SubscribeEvent(remoteObjectName, "PackageProcessed",
                                  new PackageFinishedEventHandler((s, e) =>
                                  {
                                      PackageSender ntask;

                                      lock (processingPackages)
                                      {
                                          var retVal =
                                              (from t in processingPackages where t.Package.Id == e.Package.Id select t)
                                                  .FirstOrDefault();
                                          if (retVal != null)
                                          {
                                              processingPackages.Remove(retVal);
                                          }

                                          ntask = retVal;
                                      }

                                      PackageTrigger trigger;
                                      lock (unCommittedPackages)
                                      {
                                          trigger = (from t in unCommittedPackages
                                           where t.Args.Package.Id == e.Package.Id
                                           select t).FirstOrDefault();
                                      }


                                      if (ntask != null || trigger != null)
                                      {
                                          if ((e.Tasks.All(Success) && Success(e.Package)) || !e.Tasks.Any(RequireReboot))
                                          {
                                              bool reTriggered = trigger != null;
                                              if (!reTriggered)
                                              {
                                                  lock (unCommittedPackages)
                                                  {
                                                      if (unCommittedPackages.All(t => t.Args.Package.Id != e.Package.Id))
                                                      {
                                                          unCommittedPackages.Add(new PackageTrigger
                                                                    {
                                                                        Args = e,
                                                                        LastTrigger = DateTime.MinValue
                                                                    });
                                                      }
                                                  }

                                              }

                                              if (reTriggered)
                                              {
                                                  LogEnvironment.LogDebugEvent(string.Format("The calling system did not commit the job {0} yet", e.Package.Id), LogSeverity.Warning);
                                              }
                                          }
                                          else
                                          {
                                              lock (processingPackages)
                                              {
                                                  processingPackages.Add(ntask);
                                              }

                                              RebootService();
                                          }
                                      }
                                  }));
            client.OperationalChanged += (o, e) =>
            {
                if (client.Operational)
                {
                    lock (processingPackages)
                    {
                        processingPackages.ForEach(t => t.Sent = false);
                    }
                }
                else
                {
                    timeOfCommunicationLoss = DateTime.Now;
                }
            };

            InitializeService();
            BackgroundRunner.AddPeriodicTask(ReTriggerPendingTasks,20000);
        }

        /// <summary>
        /// Retriggers uncommitted events
        /// </summary>
        private void ReTriggerPendingTasks()
        {
            if (!client.Operational && DateTime.Now.Subtract(timeOfCommunicationLoss).TotalMinutes > 2)
            {
                timeOfCommunicationLoss = DateTime.Now;
                requireReboot = true;
            }

            if (requireReboot)
            {
                RebootService();
                requireReboot = false;
                return;
            }

            PackageTrigger[] packages;
            lock (unCommittedPackages)
            {
                var tmp =
                    (from t in unCommittedPackages
                     where DateTime.Now.Subtract(t.LastTrigger).TotalMinutes > 5
                     select t).ToArray();
                tmp.ForEach(t => t.LastTrigger = DateTime.Now);
                packages = tmp;
            }

            packages.ForEach(n => OnPackageProcessed(n.Args));
            PackageSender[] openPackages;
            lock (processingPackages)
            {
                var tmp =
                    (from t in processingPackages where !t.Sent select t).ToArray();
                openPackages = tmp;
            }

            foreach (var tmp in openPackages)
            {
                var obj = tmp;
                var result = EnqueuePackageOnService(tmp.Package);
                result.ContinueWith((task, state) =>
                {
                    if (task.IsFaulted)
                    {
                        requireReboot = true;
                        return;
                    }

                    obj.Sent = true;
                }, null);
            }
        }


        /// <summary>
        /// Kills the service with the configured name
        /// </summary>
        private void KillService()
        {
            string query = string.Format(
                "SELECT ProcessId FROM Win32_Service WHERE Name='{0}'",
                serviceName);
            ManagementObjectSearcher searcher =
                new ManagementObjectSearcher(query);
            foreach (ManagementObject obj in searcher.Get())
            {
                uint processId = (uint)obj["ProcessId"];
                Process process = null;
                try
                {
                    process = Process.GetProcessById((int)processId);
                }
                catch (ArgumentException)
                {
                    LogEnvironment.LogDebugEvent(@"the process specified by processId
is no longer running.", LogSeverity.Warning);
                }
                try
                {
                    if (process != null)
                    {
                        process.Kill();
                    }
                }
                catch (Win32Exception)
                {
                    LogEnvironment.LogDebugEvent(@"process is already terminating,
the process is a Win16 exe or the process
could not be terminated.", LogSeverity.Warning);
                }
                catch (InvalidOperationException)
                {
                    LogEnvironment.LogDebugEvent("The process has probably already terminated.", LogSeverity.Warning);
                }
            }
        }

        /// <summary>
        /// Enqueues a package on the worker service
        /// </summary>
        /// <param name="package">the package that is supposed to be processed on the working service</param>
        /// <returns></returns>
        private async Task<object> EnqueuePackageOnService(IProcessPackage package)
        {
            var retVal = await client.CallRemoteMethodAsync(remoteObjectName, "EnqueuePackage", new object[] { package });
            return retVal;
        }

        /// <summary>
        /// Informs listening clients that a package that has been passed for processing is done
        /// </summary>
        public event PackageFinishedEventHandler PackageProcessed;

        /// <summary>
        /// Informs a calling class of a Disposal of this Instance
        /// </summary>
        public event EventHandler Disposed;
    }
}
