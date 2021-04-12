using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.DataExchange.KeyValueImport.Config
{
    public class CsvImportConfigurationCollection:List<CsvImportConfiguration>
    {
        public CsvImportConfiguration this[string name]
        {
            get { return this.FirstOrDefault(n => n.Name.Equals(name)); }
        }
    }
}
