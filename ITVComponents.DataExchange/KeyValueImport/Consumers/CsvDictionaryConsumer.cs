using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.DataExchange.DictionaryTableImport;
using ITVComponents.DataExchange.KeyValueImport.Config;
using ITVComponents.DataExchange.KeyValueImport.Data;
using ITVComponents.DataExchange.KeyValueImport.Decider;

namespace ITVComponents.DataExchange.KeyValueImport.Consumers
{
    public class CsvDictionaryConsumer:DefaultDictionaryConsumer<CsvDataRecord>
    {
        public CsvDictionaryConsumer(IDictionarySource<CsvDataRecord> source, ColumnNameMode columnMode, string tableName) : base(source, columnMode, tableName)
        {
        }

        public CsvDictionaryConsumer(IDictionarySource<CsvDataRecord> source, ColumnNameMode columnMode, KeyValueConfiguration config) : base(source, columnMode, config)
        {
        }

        /// <summary>
        /// Sets the value of the specified column
        /// </summary>
        /// <param name="name">the name of the column</param>
        /// <param name="value">the value that was provided from the base data provider</param>
        protected override void SetValueOfColumn(string name, CsvDataRecord value)
        {
            //throw new NotImplementedException();
            SetValueOfColumn(name,value.Converted
            ?? value.RawText);
        }

        /// <summary>
        /// Translates a specific object to a ColumnReEvaluation object
        /// </summary>
        /// <param name="value">the original data that was read from the underlaying datasource</param>
        /// <returns>a value that provides information required for deciding the need of re-evaluating column names</returns>
        protected override ColumnReEvaluationData ToColumnReEvaluationData(IDictionary<string, CsvDataRecord> value)
        {
            return new ColumnReEvaluationData(new CsvDictionaryWrapper(value));
        }

        /// <summary>
        /// Gets the string value of the given item
        /// </summary>
        /// <param name="item">the item of which to get the string value</param>
        /// <returns>the string appeareance of the given item</returns>
        protected override string GetStringValueOfItem(CsvDataRecord item)
        {
            return item.RawText;
        }
    }
}
