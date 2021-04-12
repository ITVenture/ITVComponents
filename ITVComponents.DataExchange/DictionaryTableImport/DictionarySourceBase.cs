﻿using System;
using System.Collections.Generic;
using System.Linq;
using ITVComponents.DataExchange.Import;
using ITVComponents.DataExchange.KeyValueTableImport;
using ITVComponents.ExtendedFormatting;

namespace ITVComponents.DataExchange.DictionaryTableImport
{
    public abstract class DictionarySourceBase<TValue>:ImportSourceBase<IDictionary<string,TValue>, DictionaryAcceptanceCallbackParameter<TValue>>, IDictionarySource<TValue>
    {
        #region Overrides of ImportSourceBase<IBasicKeyValueProvider,KeyValueAcceptanceCallbackParameter,IAcceptanceConstraint<IBasicKeyValueProvider>>

        /// <summary>
        /// Processes the importer response
        /// </summary>
        /// <param name="notification">the response of the importer that has accepted the input</param>
        protected override void ProcessResult(DictionaryAcceptanceCallbackParameter<TValue> notification)
        {
            string[] oriKeys = notification.SourceData.Keys.ToArray();
            string[] acceptedKeys = notification.AcceptedKeys;
            if (oriKeys.Length != acceptedKeys.Length)
            {
                string rawMsg = $@"the key(s) {string.Join(", ", from a in oriKeys.Select((s, i) => new {Index = i, Value = s})
                    join b in acceptedKeys.Select((s, i) => new {Index = i, Value = s}) on a.Value equals b.Value into c
                    from d in c.DefaultIfEmpty()
                    where d == null
                    select a.Value)} were not processed by the accepting consumer";
                LogParserEvent(notification.SourceData, rawMsg, ParserEventSeverity.Report, notification.Data);
            }
        }

        #region Overrides of ImportSourceBase<IBasicKeyValueProvider,KeyValueAcceptanceCallbackParameter,IAcceptanceConstraint<IBasicKeyValueProvider>>

        /// <summary>
        /// Notifies an inherited class that no consumer was able to process a specific portion of data that is provided by this DataSource
        /// </summary>
        /// <param name="data">the portion of Data that is not being recognized</param>
        protected override void NoMatchFor(IDictionary<string,TValue> data)
        {
            LogParserEvent(data,"No Listener found to process this record!",ParserEventSeverity.Report);
        }

        #endregion

        #endregion
    }
}
