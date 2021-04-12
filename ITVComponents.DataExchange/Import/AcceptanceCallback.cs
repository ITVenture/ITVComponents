using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using ITVComponents.DataAccess;

namespace ITVComponents.DataExchange.Import
{
    /// <summary>
    /// Acceptance Callback that is used to notify a source that its data was accepted by a consumer
    /// </summary>
    /// <typeparam name="T">the Target-Type of the Acceptance Parameter. The class must extend the AcceptanceCallbackParameter class</typeparam>
    /// <typeparam name="TData">the Data that is processed by the caller</typeparam>
    /// <param name="argument">an instance of T containing the required information of the consumer</param>
    public delegate void AcceptanceCallback<in T,TData>(T argument) where T : AcceptanceCallbackParameter<TData>;

    /// <summary>
    /// Logs an event on all Event-listeners of an ImportSource
    /// </summary>
    /// <param name="sourceData">the sourceData that was generated from this instance</param>
    /// <param name="eventText">the event-text for the parser-event</param>
    /// <param name="severity">the severity of the parser event</param>
    /// <param name="resultingData">the data that was recognized from one of the parsers or from this ImportSource</param>
    public delegate void LogParserEventCallback<in T>(T sourceData, string eventText, ParserEventSeverity severity, DynamicResult resultingData = null);

    /// <summary>
    /// Acceptance Package that is sent between a consumer and its source
    /// </summary>
    public class AcceptanceCallbackParameter<T>
    {
        /// <summary>
        /// Initializes a new instance of the AcceptanceCallbackParameter class
        /// </summary>
        /// <param name="success"></param>
        /// <param name="dataSetName">the name of the processed DataSet</param>
        /// <param name="data">the data that was recognized for the provided DataSet-name</param>
        public AcceptanceCallbackParameter(bool success, string dataSetName, DynamicResult data, T sourceData)
        {
            Success = success;
            DataSetName = dataSetName;
            Data = data;
            SourceData = sourceData;
        }

        /// <summary>
        /// Gets a value indicating whether the input was accepted by the calling importer
        /// </summary>
        public bool Success { get; }

        /// <summary>
        /// Gets the name of the DataSet for which the imported Data was recognized
        /// </summary>
        public string DataSetName { get; }

        /// <summary>
        /// Gets the imported Data for a consumer-call
        /// </summary>
        public DynamicResult Data { get; }

        /// <summary>
        /// Gets the source-Data of the processing
        /// </summary>
        public T SourceData { get; }
    }
}
