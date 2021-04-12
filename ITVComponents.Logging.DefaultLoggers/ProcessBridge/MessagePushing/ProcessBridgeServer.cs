using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ITVComponents.Plugins;

namespace ITVComponents.Logging.DefaultLoggers.ProcessBridge.MessagePushing
{
    public class ProcessBridgeServer : IPlugin, ILogBridge
    {
        /// <summary>
        /// Gets or sets the UniqueName of this Plugin
        /// </summary>
        public string UniqueName { get; set; }

        /// <summary>
        /// Logs an event through the ProcessBridge on a remote logger instance
        /// </summary>
        /// <param name="message">the message to log</param>
        /// <param name="severity">the severity of the log message</param>
        /// <param name="context">the context in which the message was created</param>
        public void LogEvent(string message, int severity, string context)
        {
            LogEnvironment.LogEvent(message, severity, context);
        }

        /// <summary>Führt anwendungsspezifische Aufgaben durch, die mit der Freigabe, der Zurückgabe oder dem Zurücksetzen von nicht verwalteten Ressourcen zusammenhängen.</summary>
        /// <filterpriority>2</filterpriority>
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
