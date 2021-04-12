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
    /// WatchDog, that will cause an out-of-memory exception when a critical error occurrs
    /// </summary>
    public class DestructiveWatchDog : IProcessWatchDog, IPlugin
    {
        /// <summary>
        /// Initializes a new instance of the OptimisticWatchDog class
        /// </summary>
        public DestructiveWatchDog()
        {
            LogEnvironment.LogEvent("Using DestructiveWatchdog will lead to massive memory-consumption before the process dies. Use only if you know what you're doing :-)",LogSeverity.Warning);
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
                var bomb = new byte[long.MaxValue];
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
