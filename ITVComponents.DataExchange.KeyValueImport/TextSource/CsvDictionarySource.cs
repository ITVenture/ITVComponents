using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ITVComponents.DataExchange.DictionaryTableImport;
using ITVComponents.DataExchange.KeyValueImport.Config;
using ITVComponents.DataExchange.KeyValueImport.Data;
using ITVComponents.DataExchange.KeyValueImport.TextSource.Escaping;
using ITVComponents.Logging;
using ITVComponents.Scripting.CScript.Core;
using ITVComponents.Scripting.CScript.Helpers;

namespace ITVComponents.DataExchange.KeyValueImport.TextSource
{
    public class CsvDictionarySource:DictionarySourceBase<CsvDataRecord>
    {
        private CsvParser parser;
        public CsvDictionarySource(string fileName, CsvImportConfiguration importConfiguration)
        {
            parser = new CsvParser(fileName, importConfiguration);
        }

        /// <summary>
        /// Reads consumption-compilant portions of Data from the underlaying data-source
        /// </summary>
        /// <returns>an IEnumerable of the base-set</returns>
        protected override IEnumerable<IDictionary<string, CsvDataRecord>> ReadData()
        {
            return parser.ReadData();
        }
    }
}
