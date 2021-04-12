using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.DataExchange.DictionaryTableImport;
using ITVComponents.DataExchange.ExcelSource.Data;
using ITVComponents.DataExchange.KeyValueImport.Config;
using ITVComponents.DataExchange.KeyValueImport.Consumers;
using ITVComponents.DataExchange.KeyValueImport.Decider;

namespace ITVComponents.DataExchange.ExcelSource.DictionaryImpl
{
    public class ExcelDataDictionaryConsumer:DefaultDictionaryConsumer<ExcelDataRecord>
    {
        public ExcelDataDictionaryConsumer(IDictionarySource<ExcelDataRecord> source, ColumnNameMode columnMode, string tableName) : base(source, columnMode, tableName)
        {
        }

        public ExcelDataDictionaryConsumer(IDictionarySource<ExcelDataRecord> source, ColumnNameMode columnMode, KeyValueConfiguration config) : base(source, columnMode, config)
        {
        }

        /// <summary>
        /// Gets or sets the ImportMode of this Consumer
        /// </summary>
        public ImportMode ImportMode { get; set; } = ImportMode.ActualTypes;

        /// <summary>
        /// Gets or sets the Prefix that is added to names when raw values are consumed
        /// </summary>
        public string RawValuePrefix { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the prefix that is added when text-only values are added
        /// </summary>
        public string TextValuePrefix { get; set; } = string.Empty;

        /// <summary>
        /// Sets the value of the specified column
        /// </summary>
        /// <param name="name">the name of the column</param>
        /// <param name="value">the value that was provided from the base data provider</param>
        protected override void SetValueOfColumn(string name, ExcelDataRecord value)
        {
            if ((ImportMode.ActualTypes & ImportMode) == ImportMode.ActualTypes)
            {
                SetValueOfColumn($"{RawValuePrefix}{name}",value?.RawData);
            }

            if ((ImportMode.Text & ImportMode) == ImportMode.Text)
            {
                SetValueOfColumn($"{TextValuePrefix}{name}", value?.FormattedText);
            }
        }

        /// <summary>
        /// Translates a specific object to a ColumnReEvaluation object
        /// </summary>
        /// <param name="value">the original data that was read from the underlaying datasource</param>
        /// <returns>a value that provides information required for deciding the need of re-evaluating column names</returns>
        protected override ColumnReEvaluationData ToColumnReEvaluationData(IDictionary<string, ExcelDataRecord> value) => new ColumnReEvaluationData(new ExcelDataDictionaryWrapper(value, ImportMode.ActualTypes));

        /// <summary>
        /// Gets the string value of the given item
        /// </summary>
        /// <param name="item">the item of which to get the string value</param>
        /// <returns>the string appeareance of the given item</returns>
        protected override string GetStringValueOfItem(ExcelDataRecord item) => item.FormattedText;
    }
}
