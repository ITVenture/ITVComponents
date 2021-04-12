using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Xml;
using ITVComponents.DataAccess;
using ITVComponents.DataAccess.Extensions;
using ITVComponents.Decisions;
using ITVComponents.Logging;

namespace ITVComponents.DataExchange.Import
{
    public abstract class ImportSourceBase<T, TNotification>: IImportSource<T,TNotification> where TNotification:AcceptanceCallbackParameter<T>
                                                                                                                                          where T: class  
    {
        /// <summary>
        /// Holds a list of attached consumers for this base-importer
        /// </summary>
        private List<IImportConsumer<T, TNotification>> consumers;

        /// <summary>
        /// Holds a list of listeners for this Importer
        /// </summary>
        [NonSerialized]
        private List<IParserEventListener> listeners;

        /// <summary>
        /// Initializes a new instance of the ImportSourceBase class
        /// </summary>
        protected ImportSourceBase()
        {
            consumers = new List<IImportConsumer<T, TNotification>>();
            listeners = new List<IParserEventListener>();
        }

        public long MaxRecordsToRead { get; set; } = 0;

        #region Implementation of IImportSource<out T,TNotification,in TAcceptanceConstraint>

        /// <summary>
        /// Registers a Textconsumer to the list of data recipients
        /// </summary>
        /// <param name="consumer">a consumer that must be triggered in order to process text that is being read from a specific source</param>
        public virtual void Register(IImportConsumer<T, TNotification> consumer)
        {
            consumers.Add(consumer);
        }

        /// <summary>
        /// Adds an eventlistener to this ImportSource
        /// </summary>
        /// <param name="listener">the event-listener that should react on events from this ImportSource</param>
        public virtual void AddListener(IParserEventListener listener)
        {
            listeners.Add(listener);
        }

        /// <summary>
        /// Starts the Consuption of data
        /// </summary>
        public virtual void StartConsumption()
        {
            using (ImportContext.BeginImport())
            {
                consumers.ForEach(n => n.ConsumptionStarts());
                LogParserEvent(default(T), "Import Starts", ParserEventSeverity.Report);
                int processedRecordCount = 0;
                foreach (T data in ReadData())
                {
                    LogParserEvent(data, "Searching capable consumer...", ParserEventSeverity.Report);
                    try
                    {
                        string msg;
                        if (
                            consumers.Count(n =>
                                ((n.AcceptanceConstraints.Decide(data, DecisionMethod.Simple, out msg) &
                                  (DecisionResult.Acceptable | DecisionResult.Success)) != DecisionResult.None) &&
                                n.Consume(data, a =>
                                {
                                    ImportContext.Current.SetCurrent(a.DataSetName, a.Data);
                                    LogParserEvent(data, "Data was parsed.", ParserEventSeverity.Report,
                                        a.Data);
                                    ProcessResult(a);
                                }, LogParserEvent)) == 0)
                        {
                            NoMatchFor(data);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogParserEvent(data, ex.Message, ParserEventSeverity.Error);
                    }
                    finally
                    {
                        processedRecordCount++;
                    }

                    if (MaxRecordsToRead != 0 && processedRecordCount > MaxRecordsToRead)
                    {
                        break;
                    }
                }

                consumers.ForEach(n => n.EndOfFile());
                if (consumers.Any(n => !n.ConsumedAnyData))
                {
                    string s = string.Join("\r\n", consumers.Where(n => !n.ConsumedAnyData).Select(n => n.AcceptanceConstraints.ToString()));
                    LogParserEvent(null, $@"Some Consumers did not consume data due to Constraint restrictions:
{s}", ParserEventSeverity.Warning, null);
                }

                LogParserEvent(default(T), "Import Ends", ParserEventSeverity.Report);
            }
        }

        #endregion

        /// <summary>
        /// Checks on all registered consumers whether a specific fragment of data is processable
        /// </summary>
        /// <param name="fragment">the fragment of registered data</param>
        /// <returns>a value indicating whether any of the attached importers is capable of importing the provided fragment</returns>
        protected bool FragmentProcessable(T fragment)
        {
            string msg;
            return consumers.Any(n => (n.AcceptanceConstraints.Decide(fragment, DecisionMethod.Simple, out msg) & (DecisionResult.Acceptable | DecisionResult.Success)) != DecisionResult.None);
        }

        /// <summary>
        /// Logs an event on all Event-listeners of this ImportSource
        /// </summary>
        /// <param name="sourceData">the sourceData that was generated from this instance</param>
        /// <param name="eventText">the event-text for the parser-event</param>
        /// <param name="severity">the severity of the parser event</param>
        /// <param name="resultingData">the data that was recognized from one of the parsers or from this ImportSource</param>
        protected void LogParserEvent(T sourceData, string eventText, ParserEventSeverity severity,
            DynamicResult resultingData = null)
        {
            listeners.ForEach(
                            n => n?.ReportEvent(sourceData, eventText, severity, resultingData));
        }

        /// <summary>
        /// Reads consumption-compilant portions of Data from the underlaying data-source
        /// </summary>
        /// <returns>an IEnumerable of the base-set</returns>
        protected abstract IEnumerable<T> ReadData();

        /// <summary>
        /// Processes the importer response
        /// </summary>
        /// <param name="notification">the response of the importer that has accepted the input</param>
        protected abstract void ProcessResult(TNotification notification);

        /// <summary>
        /// Notifies an inherited class that no consumer was able to process a specific portion of data that is provided by this DataSource
        /// </summary>
        /// <param name="data">the portion of Data that is not being recognized</param>
        protected abstract void NoMatchFor(T data);

        /// <summary>
        /// Offers the Possibility to perform Tasks during serialization of this instance
        /// </summary>
        /// <param name="context">the streaming-context</param>
        protected virtual void Serializing(StreamingContext context)
        {
        }

        /// <summary>
        /// Offers the possibility to perform Tasks during the Deserialization of this instance
        /// </summary>
        /// <param name="context">the streaming-context</param>
        protected virtual void Deserializing(StreamingContext context)
        {
            listeners = new List<IParserEventListener>();
        }

        /// <summary>
        /// Offers the possibility to perform Tasks after the deserialization of this instance
        /// </summary>
        /// <param name="context">the streaming-context</param>
        protected virtual void Deserialized(StreamingContext context)
        {
        }

        /// <summary>
        /// Handles the Serialization of this instance
        /// </summary>
        /// <param name="context">the streaming-context of this instance</param>
        [OnSerializing]
        private void OnSerializing(StreamingContext context)
        {
            Serializing(context);
        }

        /// <summary>
        /// Handles the Deserializing of this instance
        /// </summary>
        /// <param name="context">the streamingcontext of this instance</param>
        [OnDeserializing]
        private void OnDeserializing(StreamingContext context)
        {
            Deserializing(context);
        }

        /// <summary>
        /// Handles the post-deserializing of this instance
        /// </summary>
        /// <param name="context">the streamingcontext of this instance</param>
        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            Deserialized(context);
        }
    }
}
