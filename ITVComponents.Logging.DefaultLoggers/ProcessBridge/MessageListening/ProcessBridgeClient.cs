using System;
using ITVComponents.InterProcessCommunication.Shared.Base;
using ITVComponents.Plugins;

namespace ITVComponents.Logging.DefaultLoggers.ProcessBridge.MessageListening
{
    public class ProcessBridgeClient:IDisposable
    {
        /// <summary>
        /// The Logger bridge that is used to consume the logging service
        /// </summary>
        private ILogBridge bridge;

        /// <summary>
        /// Initializes a new instance of the ProcessBridgeClient class
        /// </summary>
        /// <param name="consumer">the proxy-consumer that provides access to the monitored service</param>
        /// <param name="serviceName">the name of the bridge service object</param>
        public ProcessBridgeClient(IBidirectionalClient consumer, string serviceName)
        {
            bridge = consumer.CreateProxy<ILogBridge>(serviceName);
            bridge.LogMessage += LogMessage;
        }

        /// <summary>
        /// Gets or sets a value indicating whether to enable the bridge logger
        /// </summary>
        public bool Enabled
        {
            get { return bridge.Enabled; }
            set { bridge.Enabled = value; }
        }

        /// <summary>
        /// Gets or sets the min-severity that is used by the remote logger
        /// </summary>
        public int MinSeverity { get { return bridge.MinSeverity; } set { bridge.MinSeverity = value; } }

        /// <summary>
        /// Gets or sets the Maximum severity that is used by the remote logger
        /// </summary>
        public int MaxSeverity { get { return bridge.MaxSeverity; } set { bridge.MaxSeverity = value; } }

        /// <summary>
        /// Führt anwendungsspezifische Aufgaben durch, die mit der Freigabe, der Zurückgabe oder dem Zurücksetzen von nicht verwalteten Ressourcen zusammenhängen.
        /// </summary>
        /// <filterpriority>2</filterpriority>
        public void Dispose()
        {
            bridge.LogMessage -= LogMessage;
        }

        /// <summary>
        /// Logs a message into the current logging-environment
        /// </summary>
        /// <param name="sender">the event-sender</param>
        /// <param name="e">the event arguments</param>
        private void LogMessage(object sender, LogMessageEventArgs e)
        {
            LogEnvironment.LogEvent(e.Message, e.Severity);
        }
    }
}