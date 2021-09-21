using System;
using System.Collections.Generic;
using System.Net.Mail;
using System.Text.RegularExpressions;
using ITVComponents.DataAccess;
using ITVComponents.DataExchange.Configuration;
using ITVComponents.DataExchange.Import;
using ITVComponents.DataExchange.KeyValueImport.Config;
using ITVComponents.DataExchange.KeyValueImport.Decider;
using ITVComponents.DataExchange.KeyValueTableImport;
using ITVComponents.Decisions;
using ITVComponents.ExtendedFormatting;

namespace ITVComponents.DataExchange.KeyValueImport.Consumers
{
    public class DefaultKeyValueConsumer:KeyValueConsumerBase
    {
        /// <summary>
        /// The TableName for this Consumer
        /// </summary>
        private string tableName;

        /// <summary>
        /// The Mode how columns are mapped in this consumer
        /// </summary>
        private ColumnNameMode columnMode;

        /// <summary>
        /// The Consumer configuration
        /// </summary>
        private KeyValueConfiguration config;

        /// <summary>
        /// If AutoMap is enabled, this dictionary contains the current mapping
        /// </summary>
        private Dictionary<string, string> autoMap = new Dictionary<string, string>();

        /// <summary>
        /// Initializes a new instance of the DefaultKeyValueConsumer class
        /// </summary>
        /// <param name="source">the source KeyValueSource object that provides raw-key-value data</param>
        /// <param name="columnMode">the column-mode of this consumer</param>
        /// <param name="tableName">the table that is used to register the data of this consumer</param>
        public DefaultKeyValueConsumer(
            IKeyValueSource source, ColumnNameMode columnMode, string tableName)
            : this(source, columnMode, (KeyValueConfiguration)null)
        {
            this.tableName = tableName;
        }

        /// <summary>
        /// Initializes a new instance of the DefaultKeyValueConsumer class
        /// </summary>
        /// <param name="source">the source KeyValueSource object that provides raw-key-value data</param>
        /// <param name="columnMode">the column-mode of this consumer</param>
        /// <param name="config">the Key-Value configuration used to configure this consumer</param>
        public DefaultKeyValueConsumer(IKeyValueSource source, ColumnNameMode columnMode,
            KeyValueConfiguration config):base(source, config?.VirtualColumns?? new ConstConfigurationCollection())
        {
            if (columnMode == ColumnNameMode.FromConfig && (config?.Columns?.Count ?? 0) == 0)
            {
                throw new ArgumentException("Columns required when the ColumnMode is set to 'FromConfig'!", nameof(config));
            }

            this.columnMode = columnMode;
            this.config = config;
            if (config != null)
            {
                tableName = config.TableName;
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether to force columnnames to be alphanumeric only (so, all non-alphanumeric characters are removed). This Setting is only applied when using ColumnMode.FromFirstLine
        /// </summary>
        public bool ForceAlphanumericColumnNames { get; set; } = false;

        /// <summary>
        /// Gets a value indicating whether to flush the current headers and re-evaluate them using the provided data-set
        /// </summary>
        public IDecider<ColumnReEvaluationData> ReEvaluateHeaders { get; } = new SimpleDecider<ColumnReEvaluationData>(true);

        #region Overrides of ImportConsumerBase<IBasicKeyValueProvider,KeyValueAcceptanceCallbackParameter>

        #region Overrides of ImportConsumerBase<IBasicKeyValueProvider,KeyValueAcceptanceCallbackParameter>

        /// <summary>
        /// When overridden in a derived class, this method allows to perform closing Tasks after the Consumption has finished 
        /// </summary>
        protected override void OnConsumptionComplete()
        {
            ReEvaluateHeaders.FlushContext();
            base.OnConsumptionComplete();
        }

        #endregion

        /// <summary>
        /// Consumes the provided data and provides a value indicating whether the data could be processed and a callback to create a success-notification
        /// </summary>
        /// <param name="data">the data that was extracted from an ImportSource</param>
        /// <param name="logCallback">a callback that can be used to log events on the parser-process</param>
        /// <param name="getNotification">a callback that will provide an acceptance parameter for the current parsing-unit</param>
        /// <returns>a value indicating whether the data was successfully processed</returns>
        protected override bool Consume(IBasicKeyValueProvider data, LogParserEventCallback<IBasicKeyValueProvider> logCallback, out Func<DynamicResult, KeyValueAcceptanceCallbackParameter> getNotification)
        {
            bool ok = false;
            List<string> acceptedKeys = new List<string>();
            switch (columnMode)
            {
                case ColumnNameMode.Original:
                {
                    foreach (string name in data.Keys)
                    {
                        SetValueOfColumn(name, data[name]);
                        acceptedKeys.Add(name);
                    }

                    AddCurrentRecord();
                    ok = true;
                    break;
                }

                case ColumnNameMode.FromFirstLine:
                {
                    if (!CheckFirstLine(data))
                    {
                        foreach (KeyValuePair<string, string> item in autoMap)
                        {
                            SetValueOfColumn(item.Value, data[item.Key]);
                            acceptedKeys.Add(item.Key);
                        }

                        if (data.ContainsKey("$origin"))
                        {
                            SetValueOfColumn("$origin", data["$origin"]);
                        }

                        AddCurrentRecord();
                    }

                    ok = true;
                    break;
                }

                    case ColumnNameMode.FromConfig:
                {
                    foreach (ColumnConfiguration column in config.Columns)
                    {
                        try
                        {
                            SetValueOfColumn(column.TableColumn, data[column.RawDataKey]);
                            acceptedKeys.Add(column.RawDataKey);
                        }
                        catch (Exception ex)
                        {
                            logCallback(data, $@"Unable to process the provided Recordset: {ex.Message}",
                                ParserEventSeverity.Error);
                        }
                    }

                    if (data.ContainsKey("$origin"))
                    {
                        SetValueOfColumn("$origin", data["$origin"]);
                    }
                    AddCurrentRecord();
                    ok = true;
                    break;
                }
            }

            getNotification = d => new KeyValueAcceptanceCallbackParameter(ok,tableName,d,data,acceptedKeys.ToArray());
            return ok;
        }

        /// <summary>
        /// Checks whether the columns need to be re-evaluated using the current raw-record
        /// </summary>
        /// <param name="data">the provided raw-data</param>
        /// <returns>a value indicating whether this is header-data</returns>
        public bool CheckFirstLine(IBasicKeyValueProvider data)
        {
            string msg;
            var tmp = ReEvaluateHeaders.Decide(new ColumnReEvaluationData(data), DecisionMethod.Simple, out msg);
            bool retVal = false;
            if ((tmp & (DecisionResult.Success | DecisionResult.Acceptable)) != DecisionResult.None)
            {
                retVal = true;
                autoMap.Clear();
                foreach (string column in data.Keys)
                {
                    if (column != "$origin")
                    {
                        string name = $"{data[column]}";
                        if (!string.IsNullOrEmpty(name))
                        {
                            if (Regex.IsMatch(name, @"^\d+$"))
                            {
                                name = $"@{name}";
                            }

                            if (ForceAlphanumericColumnNames)
                            {
                                name =
                                    Regex.Replace(name, @"[^\w_@]", "", RegexOptions.Singleline);
                            }

                            autoMap.Add(column, name);
                        }
                    }
                }
            }

            return retVal;
        }

        #endregion
    }
    
    public enum ColumnNameMode
    {
        /// <summary>
        /// Indicates that the provided Columns that come from the Data-Provider are left unchanged
        /// </summary>
        Original,

        /// <summary>
        /// Indicates that a config is provided that will map the column-names from the input to the output
        /// </summary>
        FromConfig,

        /// <summary>
        /// Indicates that column-names are taken from the first provided record that comes from the data-provider
        /// </summary>
        FromFirstLine
    }
}
