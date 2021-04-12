using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.DataExchange.KeyValueImport.Config;
using ITVComponents.DataExchange.KeyValueImport.Data;
using ITVComponents.DataExchange.KeyValueTableImport;
using ITVComponents.ExtendedFormatting;

namespace ITVComponents.DataExchange.KeyValueImport.TextSource
{
    public class CsvKeyValueSource:KeyValueSourceBase
    {
        private CsvParser innerSource;

        public CsvKeyValueSource(string fileName, CsvImportConfiguration importConfiguration)
        {
            innerSource = new CsvParser(fileName, importConfiguration);
        }

        protected override IEnumerable<IBasicKeyValueProvider> ReadData()
        {
            return from t in innerSource.ReadData() select new CsvDictionaryWrapper(t);
        }
    }
}
