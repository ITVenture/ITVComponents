﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ITVComponents.DataAccess;
using ITVComponents.DataAccess.Extensions;

namespace ITVComponents.DataExchange.Import.Diagnostics
{
    /// <summary>
    /// ParserEventListener implementation that is simply collecting all events that match the given severity
    /// </summary>
    /// <typeparam name="T">the Data-Source - Type</typeparam>
    public class ImportDiagnosticDataCollector:ParserEventListenerBase, IEnumerable<ParserEventRecord>
    {
        /// <summary>
        /// Holds all events that have been collected of an import
        /// </summary>
        private List<ParserEventRecord> events;

        /// <summary>
        /// Initializes a new instance of the ImportDiagnosticDataCollector class
        /// </summary>
        /// <param name="severity"></param>
        public ImportDiagnosticDataCollector(ParserEventSeverity severity) : base(severity)
        {
            events = new List<ParserEventRecord>();
        }

        #region Overrides of ParserEventListenerBase<T>

        /// <summary>
        /// Offers the possibility to process filtered Events that match the configured Severity 
        /// </summary>
        /// <param name="data">the data that leads to this event</param>
        /// <param name="message">the generated message by a parser or a data-provider</param>
        /// <param name="severity">the severity of the event</param>
        /// <param name="result">the result that was generated by the parser</param>
        protected override void AddEvent(object data, string message, ParserEventSeverity severity, DynamicResult result)
        {
            events.Add(new ParserEventRecord(data, message, severity, result));
        }

        #region Overrides of ParserEventListenerBase

        /// <summary>
        /// Dumps all Events to the target event Dumpers
        /// </summary>
        protected override void DumpAllEvents()
        {
            events.ForEach(DumpEvent);
        }

        #endregion

        #endregion

        /// <summary>
        /// Clears the events
        /// </summary>
        public void Reset()
        {
            events.Clear();
        }

        #region Implementation of IEnumerable

        /// <summary>
        /// Gibt einen Enumerator zurück, der die Auflistung durchläuft.
        /// </summary>
        /// <returns>
        /// Ein <see cref="T:System.Collections.Generic.IEnumerator`1"/>, der zum Durchlaufen der Auflistung verwendet werden kann.
        /// </returns>
        /// <filterpriority>1</filterpriority>
        public IEnumerator<ParserEventRecord> GetEnumerator()
        {
            return events.GetEnumerator();
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

        #endregion
    }
}
