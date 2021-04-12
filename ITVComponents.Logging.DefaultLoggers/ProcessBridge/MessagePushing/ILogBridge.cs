using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ITVComponents.Logging.DefaultLoggers.ProcessBridge.MessagePushing
{
    public interface ILogBridge
    {
        /// <summary>
        /// Logs an event through the ProcessBridge on a remote logger instance
        /// </summary>
        /// <param name="message">the message to log</param>
        /// <param name="severity">the severity of the log message</param>
        /// <param name="context">the context in which the message was created</param>
        void LogEvent(string message, int severity, string context);
    }
}
