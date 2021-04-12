using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.Logging;

namespace ITVComponents.Plugins.ApplicationShutdown
{
    /// <summary>
    /// Default strategy of a ProcessWatchDog, that will try to exit the application when a critical error has occurred
    /// </summary>
    public class OptimisticWatchDog:IProcessWatchDog, IPlugin
    {
        /// <summary>
        /// The PluginFactory that hosts all plugins of the process
        /// </summary>
        private PluginFactory factory;

        /// <summary>
        /// Initiates an emergency shutdown of the service if a critical error ocurred
        /// </summary>
        private Thread emergencyThread;

        /// <summary>
        /// Initializes a new instance of the OptimisticWatchDog class
        /// </summary>
        /// <param name="factory">the factory that hosts all Plugins of the current process</param>
        public OptimisticWatchDog(PluginFactory factory)
        {
            this.factory = factory;
            this.emergencyThread = new Thread(new ThreadStart(this.ExitNow));
        }

        /// <summary>
        /// Gets or sets the UniqueName of this Plugin
        /// </summary>
        public string UniqueName { get; set; }

        /// <summary>
        /// Rgisters this ProcessWatchDog instance for a specific critical component object
        /// </summary>
        /// <param name="targetComponent">the registered target object</param>
        public void RegisterFor(ICriticalComponent targetComponent)
        {
            targetComponent.CriticalError += (s, e) =>
            {
                if (this.emergencyThread.ThreadState == System.Threading.ThreadState.Unstarted)
                {
                    LogEnvironment.LogDebugEvent("Emergency Exit was triggered...", LogSeverity.Warning);
                    this.emergencyThread.Start();
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
        /// initiates an emergency exit on the current Service
        /// </summary>
        private void ExitNow()
        {
            while (!factory.Dispose(10000))
            {
            }

            Environment.Exit(-1);
        }

        /// <summary>
        /// Informs a calling class of a Disposal of this Instance
        /// </summary>
        public event EventHandler Disposed;
    }
}
