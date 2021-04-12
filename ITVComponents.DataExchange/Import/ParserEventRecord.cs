using ITVComponents.DataAccess;

namespace ITVComponents.DataExchange.Import
{
    public class ParserEventRecord
    {
        /// <summary>
        /// Initializes a new instance of the ImportDiagnosticDataSet class
        /// </summary>
        /// <param name="sourceData">the source-data that was provided by an ImportSource</param>
        /// <param name="message">the Message of this Event</param>
        /// <param name="severity">the Severity of this event</param>
        /// <param name="resultData">the ResultData that was created by the parser</param>
        public ParserEventRecord(object sourceData, string message, ParserEventSeverity severity, DynamicResult resultData)
        {
            SourceData = sourceData;
            Message = message;
            Severity = severity;
            ResultData = resultData;
        }

        /// <summary>
        /// Gets the SourceData that was provided by an ImportSource
        /// </summary>
        public object SourceData { get; }

        /// <summary>
        /// Gets the Message that was generated from the parser or the ImportSource
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Gets the Severity of this event
        /// </summary>
        public ParserEventSeverity Severity { get; }

        /// <summary>
        /// Gets the Resulting Data-object of the Parser
        /// </summary>
        public DynamicResult ResultData { get; }
    }
}
