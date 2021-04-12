using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.DataExchange.KeyValueImport.Data
{
    public class CsvDataRecord
    {
        public string RawText { get; set; }

        public object Converted { get; set; }
    }
}
