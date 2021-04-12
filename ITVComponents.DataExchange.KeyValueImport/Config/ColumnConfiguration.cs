using System;

namespace ITVComponents.DataExchange.KeyValueImport.Config
{
    [Serializable]
    public class ColumnConfiguration 
    {
        /// <summary>
        /// Gets or sets The name of the TableColumn
        /// </summary>
        public string TableColumn { get; set; }

        /// <summary>
        /// Gets or sets the RegexGroup containing the original Data
        /// </summary>
        public string RawDataKey { get; set; }

        /// <summary>
        /// Gets or sets the Expression that is used to convert the given Value to the targetType for this column
        /// </summary>
        public string ConvertExpression { get; set; }
    }
}
