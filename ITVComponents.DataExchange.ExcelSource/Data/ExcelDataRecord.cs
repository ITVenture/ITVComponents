using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.DataExchange.ExcelSource.Data
{
    public class ExcelDataRecord
    {
        public string FormattedText { get; set; }

        public object RawData { get; set; }
    }
}
//new ColumnReEvaluationData(data)