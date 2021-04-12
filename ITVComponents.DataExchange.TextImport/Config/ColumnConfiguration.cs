using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection.Emit;
using System.Text;



namespace ITVComponents.DataExchange.TextImport.Config
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
        public string RegexGroup { get; set; }

        /// <summary>
        /// Gets or sets the Expression that is used to convert the given Value to the targetType for this column
        /// </summary>
        public string ConvertExpression { get; set; }
    }
}
