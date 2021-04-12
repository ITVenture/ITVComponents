using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using ITVComponents.DataAccess;
using ITVComponents.DataExchange.Configuration;
using ITVComponents.Decisions;
using ITVComponents.Logging;
using ITVComponents.Scripting.CScript.Core;
using ITVComponents.Scripting.CScript.Core.RuntimeSafety;
using ITVComponents.Scripting.CScript.Helpers;

namespace ITVComponents.DataExchange.Import
{
    public abstract class ImportConsumerBase<T, TNotification>: IImportConsumer<T, TNotification>, IEnumerable<DynamicResult> where T : class where TNotification : AcceptanceCallbackParameter<T>
    {
        /// <summary>
        /// The Import Source that provides data the must be recognized bvy this importer
        /// </summary>
        private IImportSource<T, TNotification> source;

        /// <summary>
        /// Holds a template for the current record
        /// </summary>
        private Dictionary<string, object> currentValues = new Dictionary<string, object>();

        /// <summary>
        /// Holds the results that were parsed by this instance
        /// </summary>
        private List<DynamicResult> results = new List<DynamicResult>();

        /// <summary>
        /// Holds callbacks for processing threads with current callbacks
        /// </summary>
        [NonSerialized]
        private ThreadLocal<LogParserEventCallback<T>> logCallback = new ThreadLocal<LogParserEventCallback<T>>();

        /// <summary>
        /// Holds the current record that was newly created by an add-record call
        /// </summary>
        [NonSerialized]
        private ThreadLocal<DynamicResult> newData = new ThreadLocal<DynamicResult>(); 

        /// <summary>
        /// Indicates whether the initialization of this consumer is considered done
        /// </summary>
        private bool initialized = false;

        /// <summary>
        /// Initializes a new instance of the ImportConsumerBase class
        /// </summary>
        /// <param name="source">the importsource that is providing data for this consumer</param>
        protected ImportConsumerBase(IImportSource<T, TNotification> source):this(source, new ConstConfigurationCollection())
        { 
        }

        /// <summary>
        /// Initializes a new instance of the ImportConsumerBase class
        /// </summary>
        /// <param name="source">the importsource that is providing data for this consumer</param>
        /// <param name="virtualColumns">a set of columns that is evaluated on does not directly result from the provided Data</param>
        protected ImportConsumerBase(IImportSource<T, TNotification> source, ConstConfigurationCollection virtualColumns)
        {
            VirtualColumns = virtualColumns;
            AcceptanceConstraints = new SimpleDecider<T>(false);
            this.source = source;
            source.Register(this);
        } 

        public string KeyName { get; set; } = "RecId";


        /// <summary>
        /// Gets a value indicating whether any data has been consumed by this ImportConsumer
        /// </summary>
        public bool ConsumedAnyData { get; private set; }

        public ConstConfigurationCollection VirtualColumns { get; }

        /// <summary>
        /// Indicates whether there were already values applied to the current record
        /// </summary>
        protected bool HasOpenValues { get { return currentValues.Count != 0; } }

        #region Implementation of IImportConsumer<T,out TNotification,out TAcceptanceConstraint>

        /// <summary>
        /// Gets further informations about when the importer will Accept data to import
        /// </summary>
        public IDecider<T> AcceptanceConstraints { get; }

        /// <summary>
        /// Consumes the provided text and returns the initial character position of recognition
        /// </summary>
        /// <param name="data">the text that was extracted from a TextSource</param>
        /// <param name="acceptanceCallback">A Callback that is used by the importer to commit the acceptance of a Part of the delivered data</param>
        /// <param name="logCallback">a callback that can be used to log events on the parser-process</param>
        /// <returns>a value indicating whether the data was successfully processed</returns>
        public bool Consume(T data, AcceptanceCallback<TNotification, T> acceptanceCallback, LogParserEventCallback<T> logCallback)
        {
            this.logCallback.Value = logCallback;
            bool success = false;
            Func<DynamicResult, TNotification> getNotification = null;
            try
            {
                success = Consume(data, logCallback, out getNotification);
                ConsumedAnyData |= success;
            }
            finally
            {
                this.logCallback.Value = null;
                if (success && getNotification != null)
                {
                    acceptanceCallback(getNotification(newData.Value));
                }

                newData.Value = null;
            }

            return success;
        }

        /// <summary>
        /// Is being called from the ImportSource when the Consuption of data is being started
        /// </summary>
        public void ConsumptionStarts()
        {
            ConsumedAnyData = false;
            OnConsuptionStarted();
        }

        /// <summary>
        /// Indicates that the End of the underlaying file was reached and therefore the current record has finished.
        /// </summary>
        public void EndOfFile()
        {
            if (currentValues.Count != 0)
            {
                LogEnvironment.LogDebugEvent(null, "Entity may be incomplete! Please check Configuration", (int) LogSeverity.Warning, null);
                results.Add(new DynamicResult(currentValues));
                currentValues.Clear();
            }

            OnConsumptionComplete();
            initialized = true;
        }

        /// <summary>
        /// Consumes the provided data and provides a value indicating whether the data could be processed and a callback to create a success-notification
        /// </summary>
        /// <param name="data">the data that was extracted from an ImportSource</param>
        /// <param name="logCallback">a callback that can be used to log events on the parser-process</param>
        /// <param name="getNotification">a callback that will provide an acceptance parameter for the current parsing-unit</param>
        /// <returns>a value indicating whether the data was successfully processed</returns>
        protected abstract bool Consume(T data, LogParserEventCallback<T> logCallback,
            out Func<DynamicResult, TNotification> getNotification);

        /// <summary>
        /// Sets a value for the current record
        /// </summary>
        /// <param name="columnName">the columnName</param>
        /// <param name="value">the value of the column</param>
        protected void SetValueOfColumn(string columnName, object value)
        {
            if (!currentValues.ContainsKey(columnName))
            {
                currentValues.Add(columnName, value);
            }
            else
            {
                if (currentValues[columnName] != null &&
                    !currentValues[columnName].Equals(value))
                {
                    LogEnvironment.LogDebugEvent(null,
                        string.Format(
                            "Overwriting Value of {0}. Old value: {1}, New value: {2}. If this behavior is unexpected, please correct config!",
                            columnName, currentValues[columnName], value),
                        (int) LogSeverity.Warning, null);
                    currentValues[columnName] = value;
                }
            }
        }

        /// <summary>
        /// When overridden in a derived class, this method allows to perform initial Tasks before the effective Consuption is performed
        /// </summary>
        protected virtual void OnConsuptionStarted()
        {
        }

        /// <summary>
        /// When overridden in a derived class, this method allows to perform closing Tasks after the Consumption has finished 
        /// </summary>
        protected virtual void OnConsumptionComplete()
        {
        }

        /// <summary>
        /// Registers the open data to the list of data for this consumer
        /// </summary>
        protected void AddCurrentRecord()
        {
            currentValues.Add(KeyName, results.Count);
            if (VirtualColumns.Count != 0)
            {
                Scope scope =
                    new Scope(new Dictionary<string, object>
                    {
                        {"current", currentValues},
                        {"context", ImportContext.Current}
                    });
                using (var session = ExpressionParser.BeginRepl(scope, a => { DefaultCallbacks.PrepareDefaultCallbacks(a.Scope, a.ReplSession); }))
                {
                    foreach (ConstConfiguration constConfiguration in VirtualColumns)
                    {
                        currentValues.Add(constConfiguration.Name,
                            constConfiguration.ConstType == ConstType.SingleExpression
                                ? ExpressionParser.Parse(constConfiguration.ValueExpression, session)
                                : ExpressionParser.ParseBlock(constConfiguration.ValueExpression, session));
                    }
                }
            }

            DynamicResult record = new DynamicResult(currentValues);
            newData.Value = record;
            results.Add(record);
            currentValues.Clear();
        }

        /// <summary>
        /// Ignores the current data and logs a warning telling a user-class that the specified result is being ignored
        /// </summary>
        /// <param name="data">the data that will not be processed</param>
        protected void DismissCurrentRecord(T data)
        {
            logCallback.Value(data,
                "Entity is being ignored. Please check the result and correct the source data or the indexer configuration.",
                ParserEventSeverity.Warning, new DynamicResult(currentValues));
            currentValues.Clear();
        }

        #endregion

        #region Implementation of IEnumerable

        /// <summary>
        /// Gibt einen Enumerator zurück, der die Auflistung durchläuft.
        /// </summary>
        /// <returns>
        /// Ein <see cref="T:System.Collections.Generic.IEnumerator`1"/>, der zum Durchlaufen der Auflistung verwendet werden kann.
        /// </returns>
        /// <filterpriority>1</filterpriority>
        public IEnumerator<DynamicResult> GetEnumerator()
        {
            if (!initialized)
            {
                LogEnvironment.LogDebugEvent(null, "Starting Consumption...", (int) LogSeverity.Report, null);
                source.StartConsumption();
            }

            return results.GetEnumerator();
        }

        /// <summary>
        /// Gibt einen Enumerator zurück, der eine Auflistung durchläuft.
        /// </summary>
        /// <returns>
        /// Ein <see cref="T:System.Collections.IEnumerator"/>-Objekt, das zum Durchlaufen der Auflistung verwendet werden kann.
        /// </returns>
        /// <filterpriority>2</filterpriority>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

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
            logCallback = new ThreadLocal<LogParserEventCallback<T>>();
            newData = new ThreadLocal<DynamicResult>();
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

        #endregion
    }
}
