using System;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace ITVComponents.Logging.DefaultLoggers.ProcessBridge.MessageListening
{
    /// <summary>
    /// A message forwarded by the process bridge. The properties are serialized directly.
    /// </summary>
    [ITVComponents.Json.TransferByValue]
    public class LogMessageEventArgs:EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the LogMessageEventArgs class
        /// </summary>
        /// <param name="message">the message that is being logged</param>
        /// <param name="severity">the log severity for this message</param>
        /// <param name="context">provides additional information about the context in which the message was generated</param>
        public LogMessageEventArgs(string message, int severity, string context)
        {
            Message = message;
            Severity = severity;
            Context = context;
        }

        [JsonConstructor]
        private LogMessageEventArgs()
        {

        }

        /// <summary>
        /// Gets the severity of this logged message
        /// </summary>
        [JsonInclude]
        public int Severity { get; private set; }

        /// <summary>
        /// Gets the content of this logged message
        /// </summary>
        [JsonInclude]
        public string Message { get; private set; }

        /// <summary>
        /// Gets or sets the context in which this message was created
        /// </summary>
        [JsonInclude]
        public string Context { get; private set; }
    }

    /// <summary>
    /// EventHandler for the LogMessage - Event of the ProcessorBridge composition
    /// </summary>
    /// <param name="sender">the event-sender</param>
    /// <param name="e">the event arguments</param>
    public delegate void LogMessageEventHandler(object sender, LogMessageEventArgs e);
}
