using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ITVComponents.DataAccess;
using ITVComponents.DataExchange.Configuration;
using ITVComponents.DataExchange.DictionaryTableImport;
using ITVComponents.DataExchange.Import;
using ITVComponents.DataExchange.KeyValueImport.Config;
using ITVComponents.DataExchange.KeyValueImport.Decider;
using ITVComponents.DataExchange.KeyValueTableImport;
using ITVComponents.Decisions;
using ITVComponents.ExtendedFormatting;

namespace ITVComponents.DataExchange.KeyValueImport.Consumers
{
    public abstract class DefaultDictionaryConsumer<TValue>:DictionaryConsumerBase<TValue>
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
        private Dictionary<string, List<string>> autoMap = new Dictionary<string, List<string>>();

        /// <summary>
        /// Initializes a new instance of the DefaultKeyValueConsumer class
        /// </summary>
        /// <param name="source">the source KeyValueSource object that provides raw-key-value data</param>
        /// <param name="columnMode">the column-mode of this consumer</param>
        /// <param name="tableName">the table that is used to register the data of this consumer</param>
        public DefaultDictionaryConsumer(
            IDictionarySource<TValue> source, ColumnNameMode columnMode, string tableName)
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
        public DefaultDictionaryConsumer(IDictionarySource<TValue> source, ColumnNameMode columnMode,
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
        /// Gets a list of Substitution rules that is applied to every columnName
        /// </summary>
        public IList<SubstitutionRule> ColumnNameSubstitutionRules { get; } = new List<SubstitutionRule>();

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

        /// <summary>
        /// Sets the value of the specified column
        /// </summary>
        /// <param name="name">the name of the column</param>
        /// <param name="value">the value that was provided from the base data provider</param>
        protected abstract void SetValueOfColumn(string name, TValue value);

        /// <summary>
        /// Translates a specific object to a ColumnReEvaluation object
        /// </summary>
        /// <param name="value">the original data that was read from the underlaying datasource</param>
        /// <returns>a value that provides information required for deciding the need of re-evaluating column names</returns>
        protected abstract ColumnReEvaluationData ToColumnReEvaluationData(IDictionary<string, TValue> value);

        /// <summary>
        /// Gets the string value of the given item
        /// </summary>
        /// <param name="item">the item of which to get the string value</param>
        /// <returns>the string appeareance of the given item</returns>
        protected abstract string GetStringValueOfItem(TValue item);

        #endregion

        /// <summary>
        /// Consumes the provided data and provides a value indicating whether the data could be processed and a callback to create a success-notification
        /// </summary>
        /// <param name="data">the data that was extracted from an ImportSource</param>
        /// <param name="logCallback">a callback that can be used to log events on the parser-process</param>
        /// <param name="getNotification">a callback that will provide an acceptance parameter for the current parsing-unit</param>
        /// <returns>a value indicating whether the data was successfully processed</returns>
        protected override bool Consume(IDictionary<string, TValue> data, LogParserEventCallback<IDictionary<string,TValue>> logCallback, out Func<DynamicResult, DictionaryAcceptanceCallbackParameter<TValue>> getNotification)
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
                        foreach (KeyValuePair<string, List<string>> item in autoMap)
                        {
                            TValue vl = default(TValue);
                            if (data.ContainsKey(item.Key))
                            {
                                vl = data[item.Key];
                            }

                            foreach (var outKey in item.Value)
                            {
                                SetValueOfColumn(outKey, vl);
                            }

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

            getNotification = d => new DictionaryAcceptanceCallbackParameter<TValue>(ok,tableName,d,data,acceptedKeys.ToArray());
            return ok;
        }

        /// <summary>
        /// Checks whether the columns need to be re-evaluated using the current raw-record
        /// </summary>
        /// <param name="data">the provided raw-data</param>
        /// <returns>a value indicating whether this is header-data</returns>
        public bool CheckFirstLine(IDictionary<string, TValue> data)
        {
            string msg;
            var tmp = ReEvaluateHeaders.Decide(ToColumnReEvaluationData(data), DecisionMethod.Simple, out msg);
            bool retVal = false;
            if ((tmp & (DecisionResult.Success | DecisionResult.Acceptable)) != DecisionResult.None)
            {
                retVal = true;
                autoMap.Clear();
                HashSet<string> nameHelper = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (string column in data.Keys)
                {
                    if (column != "$origin")
                    {
                        string name = GetStringValueOfItem(data[column]);
                        if (name != null)
                        {
                            autoMap.Add(column, new List<string>());
                            var globalSubst = ColumnNameSubstitutionRules.Where(n => string.IsNullOrEmpty(n.GroupTag)).ToArray();
                            name = ProcessRuleSet(globalSubst, name);
                            var groupedSubst = (from t in ColumnNameSubstitutionRules
                                where !string.IsNullOrEmpty(t.GroupTag)
                                group t by t.GroupTag
                                into g
                                select g).ToArray();
                            if (groupedSubst.Length != 0)
                            {
                                foreach (var g in groupedSubst)
                                {
                                    var n = ProcessRuleSet(g, name);
                                    if (!nameHelper.Contains(n))
                                    {
                                        autoMap[column].Add(n);
                                        nameHelper.Add(n);
                                    }
                                }
                            }
                            else
                            {
                                if (nameHelper.Contains(name))
                                {
                                    int id = 1;
                                    string repName = $"{name}{id}";
                                    while (nameHelper.Contains(repName))
                                    {
                                        id++;
                                        repName = $"{name}{id}";
                                    }
                                    name = repName;
                                }

                                autoMap[column].Add(name);
                                nameHelper.Add(name);
                            }
                        }
                    }
                }
            }

            return retVal;
        }

        private string ProcessRuleSet(IEnumerable<SubstitutionRule> substitutionSet, string name)
        {
            var retVal = name;
            foreach (SubstitutionRule rule in substitutionSet)
            {
                retVal = Regex.Replace(retVal, rule.RegexPattern, rule.ReplaceValue, rule.RegexOptions);
                if (!string.IsNullOrEmpty(rule.ReplaceValue))
                {
                    while (retVal.StartsWith(rule.ReplaceValue))
                    {
                        retVal = retVal.Substring(1);
                    }
                    while (retVal.EndsWith(rule.ReplaceValue))
                    {
                        retVal = retVal.Substring(0, retVal.Length - 1);
                    }
                }
            }
            return retVal;
        }
        #endregion
    }
}
