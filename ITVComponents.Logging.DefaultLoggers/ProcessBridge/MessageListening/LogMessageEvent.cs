using System;
using System.Runtime.Serialization;

namespace ITVComponents.Logging.DefaultLoggers.ProcessBridge.MessageListening
{
    [Serializable]
    public class LogMessageEventArgs:EventArgs, ISerializable
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

        public LogMessageEventArgs(SerializationInfo info, StreamingContext context)
        {
            Message = (string)info.GetValue(nameof(Message), typeof(string));
            Severity = (int) info.GetValue(nameof(Severity), typeof(int));
            Context = (string) info.GetValue(nameof(Context), typeof(string));
        }

        /// <summary>
        /// Gets the severity of this logged message
        /// </summary>
        public int Severity { get; private set; }

        /// <summary>
        /// Gets the content of this logged message
        /// </summary>
        public string Message { get; private set; }

        /// <summary>
        /// Gets or sets the context in which this message was created
        /// </summary>
        public string Context { get; private set; }

        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue(nameof(Message), Message);
            info.AddValue(nameof(Severity), Severity);
            info.AddValue(nameof(Context), Context);
        }
    }

    /// <summary>
    /// EventHandler for the LogMessage - Event of the ProcessorBridge composition
    /// </summary>
    /// <param name="sender">the event-sender</param>
    /// <param name="e">the event arguments</param>
    public delegate void LogMessageEventHandler(object sender, LogMessageEventArgs e);
}
