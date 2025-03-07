using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using ITVComponents.Json.Contracts;

namespace ITVComponents.Logging.DefaultLoggers.ProcessBridge.MessageListening
{
    public class LogMessageEventArgs:EventArgs, IManualSerializer
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
        public int Severity { get; private set; }

        /// <summary>
        /// Gets the content of this logged message
        /// </summary>
        public string Message { get; private set; }

        /// <summary>
        /// Gets or sets the context in which this message was created
        /// </summary>
        public string Context { get; private set; }

        public IList<ManualSerializationData> Data { get; set; }
        public void GetObjectData()
        {
            Data.Add(ManualSerializationData.FromValue(nameof(Message), Message));
            Data.Add(ManualSerializationData.FromValue(nameof(Severity), Severity));
            Data.Add(ManualSerializationData.FromValue(nameof(Context), Context));
        }

        public void ApplyObjectData()
        {
            Message = Data.GetDeserializedValue<string>(nameof(Message));
            Severity = Data.GetDeserializedValue<int>(nameof(Severity));
            Context = Data.GetDeserializedValue<string>(nameof(Context));
        }
    }

    /// <summary>
    /// EventHandler for the LogMessage - Event of the ProcessorBridge composition
    /// </summary>
    /// <param name="sender">the event-sender</param>
    /// <param name="e">the event arguments</param>
    public delegate void LogMessageEventHandler(object sender, LogMessageEventArgs e);
}
