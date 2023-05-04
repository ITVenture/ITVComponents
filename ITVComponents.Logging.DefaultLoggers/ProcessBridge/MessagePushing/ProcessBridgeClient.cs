using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ITVComponents.InterProcessCommunication.Shared.Base;

namespace ITVComponents.Logging.DefaultLoggers.ProcessBridge.MessagePushing
{
    public class ProcessBridgeClient:LogTarget
    {
        /// <summary>
        /// A client object that provides the communication to a remote service
        /// </summary>
        private IBaseClient simpleClient;

        /// <summary>
        /// the LogBridge object that is used to push messages to a remote service
        /// </summary>
        private ILogBridge loggerObject;

        /// <summary>
        /// indicates whether this bridge is currently available and able to process requests
        /// </summary>
        private bool active = false;

        public ProcessBridgeClient(IBaseClient simpleClient, string remoteLoggerName, int minSeverity, int maxSeverity, string contextFilter, bool initialStatus)
            : base(minSeverity, maxSeverity, contextFilter, initialStatus, true)
        {
            this.simpleClient = simpleClient;
            try
            {
                if (simpleClient.Operational)
                {
                    loggerObject = simpleClient.CreateProxy<ILogBridge>(remoteLoggerName);
                    active = true;
                }
            }
            catch (Exception ex)
            {
                LogEnvironment.LogEvent($"Failed to initialize remote-logger ({remoteLoggerName}@{simpleClient.UniqueName}): {ex.Message}", LogSeverity.Error);
            }
        }

        public ProcessBridgeClient(IBaseClient simpleClient, string remoteLoggerName, LogSeverity minSeverity, LogSeverity maxSeverity, string contextFilter, bool initialStatus)
            : base(minSeverity, maxSeverity, contextFilter, initialStatus, true)
        {
            this.simpleClient = simpleClient;
            try
            {
                if (simpleClient.Operational)
                {
                    loggerObject = simpleClient.CreateProxy<ILogBridge>(remoteLoggerName);
                    active = true;
                }
            }
            catch (Exception ex)
            {
                LogEnvironment.LogEvent($"Failed to initialize remote-logger ({remoteLoggerName}@{simpleClient.UniqueName}): {ex.Message}", LogSeverity.Error);
            }
        }

        public ProcessBridgeClient(IBaseClient simpleClient, string remoteLoggerName, int minSeverity, int maxSeverity, string contextFilter, bool initialStatus, bool debugEnabled)
            : base(minSeverity, maxSeverity, contextFilter, initialStatus, debugEnabled, true)
        {
            this.simpleClient = simpleClient;
            try
            {
                if (simpleClient.Operational)
                {
                    loggerObject = simpleClient.CreateProxy<ILogBridge>(remoteLoggerName);
                    active = true;
                }
            }
            catch (Exception ex)
            {
                LogEnvironment.LogEvent($"Failed to initialize remote-logger ({remoteLoggerName}@{simpleClient.UniqueName}): {ex.Message}", LogSeverity.Error);
            }
        }

        public ProcessBridgeClient(IBaseClient simpleClient, string remoteLoggerName, LogSeverity minSeverity, LogSeverity maxSeverity, string contextFilter, bool initialStatus, bool debugEnabled)
            : base(minSeverity, maxSeverity, contextFilter, initialStatus, debugEnabled, true)
        {
            this.simpleClient = simpleClient;
            try
            {
                if (simpleClient.Operational)
                {
                    loggerObject = simpleClient.CreateProxy<ILogBridge>(remoteLoggerName);
                    active = true;
                }
            }
            catch (Exception ex)
            {
                LogEnvironment.LogEvent($"Failed to initialize remote-logger ({remoteLoggerName}@{simpleClient.UniqueName}): {ex.Message}", LogSeverity.Error);
            }
        }

        /// <summary>
        /// Can be used to provide further information about the Status of this logger
        /// </summary>
        /// <returns>a value indicating whether the logger is ready to perform loggin operations</returns>
        protected override bool IsEnabled()
        {
            return active && simpleClient.Operational;
        }

        /// <summary>
        /// Logs an event to this Log-Target
        /// </summary>
        /// <param name="eventText">the event-text</param>
        /// <param name="severity">the severity of the event</param>
        /// <param name="context">provides additional information about the logging-context in which the message was generated</param>
        protected override void Log(string eventText, int severity, string context)
        {
            loggerObject.LogEvent(eventText, severity, context);
        }
    }
}
