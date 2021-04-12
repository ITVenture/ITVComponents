using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.InterProcessCommunication.Shared.Base;
using ITVComponents.Logging;
using ITVComponents.Plugins;

namespace ITVComponents.InterProcessCommunication.Shared.WatchDogs
{
    /// <summary>
    /// A Local ProcessWatchDog implementation that will used an external process for the reboot of the process
    /// </summary>
    public class LocalProcessWatchDog:IPlugin, IDeferredInit, IProcessWatchDog
    {
        /// <summary>
        /// the interprocess client that enables this instance to alert an invalid process state
        /// </summary>
        private readonly IBaseClient client;

        /// <summary>
        /// the object that will listen to alerts
        /// </summary>
        private readonly string objectName;

        /// <summary>
        /// The remote watchdog that will monitor this process
        /// </summary>
        private IIpcWatchDog remoteWatchDog;

        /// <summary>
        /// The name of the local machine
        /// </summary>
        private string machineName;

        /// <summary>
        /// the id of the current process
        /// </summary>
        private int processId;

        /// <summary>
        /// the name of the current process
        /// </summary>
        private string processName;

        /// <summary>
        /// Initializes a new instance of the LocalProcessWatchDog class
        /// </summary>
        /// <param name="client">the interprocess client that enables this instance to alert an invalid process state</param>
        /// <param name="objectName">the object that will listen to alerts</param>
        public LocalProcessWatchDog(IBaseClient client, string objectName)
        {
            this.client = client;
            this.objectName = objectName;
        }

        /// <summary>
        /// Gets or sets the UniqueName of this Plugin
        /// </summary>
        public string UniqueName { get; set; }

        /// <summary>
        /// Indicates whether this deferrable init-object is already initialized
        /// </summary>
        public bool Initialized { get; private set; } = false;

        /// <summary>
        /// Indicates whether this Object requires immediate Initialization right after calling the constructor
        /// </summary>
        public bool ForceImmediateInitialization { get; } = false;

        /// <summary>
        /// Initializes this deferred initializable object
        /// </summary>
        public void Initialize()
        {
            if (!Initialized)
            {
                remoteWatchDog = client.CreateProxy<IIpcWatchDog>(objectName);
                var proc = Process.GetCurrentProcess();
                processId = proc.Id;
                processName = proc.ProcessName;
                machineName = proc.MachineName;
                remoteWatchDog.SetProcessStatus(machineName, processName, processId, false);
                Initialized = true;
            }
        }

        /// <summary>
        /// Rgisters this ProcessWatchDog instance for a specific critical component object
        /// </summary>
        /// <param name="targetComponent">the registered target object</param>
        public void RegisterFor(ICriticalComponent targetComponent)
        {
            targetComponent.CriticalError += (s, e) =>
            {
                if (Initialized)
                {
                    LogEnvironment.LogDebugEvent("Sending request to remote-Watchdog to end this process...", LogSeverity.Warning);
                    remoteWatchDog.SetProcessStatus(machineName, processName, processId, true);
                }
                else
                {
                    LogEnvironment.LogEvent("Initializeation probably failed. RemoteWatchdog is not available", LogSeverity.Error);
                }
            };
        }

        /// <summary>
        ///   Führt anwendungsspezifische Aufgaben durch, die mit der Freigabe, der Zurückgabe oder dem Zurücksetzen von nicht verwalteten Ressourcen zusammenhängen.
        /// </summary>
        public void Dispose()
        {
            OnDisposed();
        }

        /// <summary>
        /// Raises the Disposed event
        /// </summary>
        protected virtual void OnDisposed()
        {
            Disposed?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Informs a calling class of a Disposal of this Instance
        /// </summary>
        public event EventHandler Disposed;
    }
}
