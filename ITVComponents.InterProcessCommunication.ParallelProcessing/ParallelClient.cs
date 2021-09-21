using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading;
using ITVComponents.InterProcessCommunication.ParallelProcessing.Resources;
using ITVComponents.InterProcessCommunication.Shared.Base;
using ITVComponents.InterProcessCommunication.Shared.Helpers;
using ITVComponents.Logging;
using ITVComponents.ParallelProcessing;
using ITVComponents.Plugins;
using ITVComponents.Serialization;
using ITVComponents.Threading;

namespace ITVComponents.InterProcessCommunication.ParallelProcessing
{
    public abstract class ParallelClient<TPackage, TLocalEventArgs, TTask>:IDisposable, IOperationalProvider where TPackage:IProcessPackage
                                                                                       where TLocalEventArgs:EventArgs
                                                                                       where TTask:IProcessTask
    {
        /// <summary>
        /// connects this ParallelClient object to its service
        /// </summary>
        private IBidirectionalClient client;

        /// <summary>
        /// the identification of the local system
        /// </summary>
        private string localSystemIdentifier;

        /// <summary>
        /// the unique name of the remoteObject that is able to process this objects requests
        /// </summary>
        private string remoteObjectName;

        /// <summary>
        /// indicates whether this parallelClient is connected to a service
        /// </summary>
        private bool connected;

        /// <summary>
        /// eventHandler for the PackageProcessed event on the service
        /// </summary>
        private PackageFinishedEventHandler packageFinishedHandler;

        /// <summary>
        /// a timer object that checks the connection to the remote service periodically
        /// </summary>
        private Timer checkConnectionTimer;

        /// <summary>
        /// the timeout whithin the connection to the server is being tested periodically
        /// </summary>
        private int CheckTimeout = 10000;

        /// <summary>
        /// tells the timer to stop working
        /// </summary>
        private ManualResetEvent stopEvent;

        /// <summary>
        /// Enables the timer to confirm that it has stopped
        /// </summary>
        private ManualResetEvent stoppedEvent;

        /// <summary>
        /// indicates whether the state of this object is initial
        /// </summary>
        private bool initial = true;

        /// <summary>
        /// Indicates whether this object is disposed or about to..
        /// </summary>
        private bool disposed = false;

        /// <summary>
        /// Initializes a new instance of the ParallelClient class
        /// </summary>
        /// <param name="client">the base client that supports connecting the remote service</param>
        /// <param name="remoteObjectName">the object that is able to process this client's tasks</param>
        /// <param name="localSystemIdentifier">the identification of the local system</param>
        protected ParallelClient(IBidirectionalClient client, string remoteObjectName, string localSystemIdentifier)
        {
            stopEvent = new ManualResetEvent(false);
            stoppedEvent = new ManualResetEvent(false);
            this.checkConnectionTimer = new Timer(TestServer,string.Format("::{0}::",GetHashCode()),Timeout.Infinite,Timeout.Infinite);
            this.remoteObjectName = remoteObjectName;
            this.localSystemIdentifier = localSystemIdentifier;
            this.client = client;
            client.OperationalChanged += HandleClientOperationalChanged;
            packageFinishedHandler = new PackageFinishedEventHandler(PackageProcessed);
            if (!CheckConnection())
            {
            }

            checkConnectionTimer.Change(CheckTimeout, CheckTimeout);
        }

        /// <summary>
        /// Gets a value indicating whether this object is operational
        /// </summary>
        public bool Operational { get { return connected; } }

        /// <summary>
        /// Führt anwendungsspezifische Aufgaben durch, die mit der Freigabe, der Zurückgabe oder dem Zurücksetzen von nicht verwalteten Ressourcen zusammenhängen.
        /// </summary>
        /// <filterpriority>2</filterpriority>
        public virtual void Dispose()
        {
            if (!disposed)
            {
                disposed = true;
                stopEvent.Set();
                stoppedEvent.WaitOne();
                checkConnectionTimer.Dispose();
                stopEvent.Dispose();
                stoppedEvent.Dispose();
                client.UnSubscribeEvent(remoteObjectName, ParallellResources.ParallelMethod_PackageProcessed,
                                        packageFinishedHandler);
                client.OperationalChanged -= HandleClientOperationalChanged;
            }
        }

        /// <summary>
        /// Sends a package to the processing service
        /// </summary>
        /// <param name="package">the package that needs to be processed</param>
        protected void EnqueuePackage(TPackage package)
        {
            if (!CheckConnection())
            {
                throw new InvalidOperationException(ParallellResources.ParallelClient_Not_connected_to_a_service);
            }

            if (package.HasTasks)
            {
                package.RequestingSystem = localSystemIdentifier;

                try
                {
                    var dummy = client.CallRemoteMethod(remoteObjectName,
                        ParallellResources.ParallelMethod_EnqueuePackage,
                        new object[] {package});
                }
                catch(Exception ex)
                {
                    connected = false;
                    throw new InterProcessException(
                        ParallellResources.ParallelClient_EnqueuePackage_Remote_Call_failed,
                        ex);
                }
            }
            else
            {
                OnPackageProcessed(GetEventData(package, new TTask[0]));
            }
        }

        /// <summary>
        /// Raises the PackageProcessed event for this client
        /// </summary>
        /// <param name="packageFinishedEventArgs">the event arguments identifying this clients packages</param>
        protected virtual void OnPackageProcessed(TLocalEventArgs packageFinishedEventArgs)
        {
            if (PackageFinished != null)
            {
                PackageFinished(this, packageFinishedEventArgs);
            }
        }

        /// <summary>
        /// Generates the event-data from the response of the service
        /// </summary>
        /// <param name="srcPackage">the source package that has been processed by a different process</param>
        /// <param name="tasks">the tasklist that was generated by the package</param>
        /// <returns>event arguments informign the caller about the finished package</returns>
        protected abstract TLocalEventArgs GetEventData(TPackage srcPackage, TTask[] tasks);

        /// <summary>
        /// Raises the OperationalChanged event
        /// </summary>
        protected virtual void OnOperationalChanged()
        {
            EventHandler handler = OperationalChanged;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        /// <summary>
        /// Processes the PackageProcessed event and re-raises it for this clients clients
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PackageProcessed(object sender, PackageFinishedEventArgs e)
        {
            if (e.Package.RequestingSystem == localSystemIdentifier)
            {
                TLocalEventArgs args = GetEventData((TPackage)e.Package,
                                                (from t in e.Tasks select (TTask)t).ToArray());
                OnPackageProcessed(args);
                client.CallRemoteMethod(remoteObjectName, ParallellResources.ParallelMethod_CommitTaskDoneRecieved,
                                        new object[] { localSystemIdentifier, e.Package.Id });
            }
        }

        /// <summary>
        /// Tries to connect to the server and returns a value indicating whether the demanded object exists
        /// </summary>
        /// <returns>a value indicating whether the remote object exists</returns>
        private bool CheckConnection()
        {
            lock (client.Sync)
            {
                if (!connected)
                {
                    if (client.ValidateConnection())
                    {
                        var result = client.CheckRemoteObjectAvailability(remoteObjectName);
                        if (result.Available)
                        {
                            connected = true;
                            if (initial)
                            {
                                client.SubscribeEvent(remoteObjectName,
                                    ParallellResources
                                        .ParallelMethod_PackageProcessed,
                                    packageFinishedHandler);
                                initial = false;
                            }
                        }
                        else
                        {
                            LogEnvironment.LogDebugEvent(result.Message, LogSeverity.Warning);
                        }
                    }
                }

                return connected;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="state"></param>
        private void TestServer(object state)
        {
            bool reactivate = true;
            checkConnectionTimer.Change(Timeout.Infinite, Timeout.Infinite);
            try
            {
                if (!stopEvent.WaitOne(5))
                {
                    stoppedEvent.Reset();
                    lock (client)
                    {
                        bool b = connected;
                        if (connected)
                        {
                            connected = client.ValidateConnection();
                        }
                        else
                        {
                            CheckConnection();
                        }

                        if (b != connected)
                        {
                            OnOperationalChanged();
                        }
                    }
                }
                else
                {
                    reactivate = false;
                }
            }
            finally
            {
                if (reactivate && client.Operational)
                {
                    checkConnectionTimer.Change(CheckTimeout, CheckTimeout);
                }
                else
                {
                    stoppedEvent.Set();
                }
            }
        }

        /// <summary>
        /// Handles the OperationalChanged event of the IPC - Client
        /// </summary>
        /// <param name="sender">the event-sender</param>
        /// <param name="e">the event arguments</param>
        private void HandleClientOperationalChanged(object sender, EventArgs e)
        {
            if (client.Operational)
            {
                checkConnectionTimer.Change(CheckTimeout, CheckTimeout);
            }
            else
            {
                checkConnectionTimer.Change(Timeout.Infinite, Timeout.Infinite);
            }
        }

        /// <summary>
        /// Notifies client objects that a package that was sent to a different process has been processed successfully
        /// </summary>
        public event EventHandler<TLocalEventArgs> PackageFinished;

        /// <summary>
        /// Is Raised when the value for the OperationalFlag has changed
        /// </summary>
        public event EventHandler OperationalChanged;
    }
}
