using System.Collections.Generic;
using System.IO;
using ITVComponents.DataAccess;
using ITVComponents.DataExchange.Configuration;
using ITVComponents.Plugins;

namespace ITVComponents.DataExchange.Interfaces
{
    public interface IDataDumper
    {
        /// <summary>
        /// Dumps collected data into the given file
        /// </summary>
        /// <param name="fileName">the name of the target filename for this dump-run</param>
        /// <param name="data">the data that must be dumped</param>
        /// <param name="configuration">the dumper-configuiration</param>
        /// <returns>a value indicating whether there was any data available for dumping</returns>
        bool DumpData(string fileName, DynamicResult[] data, DumpConfiguration configuration);

        /// <summary>
        /// Dumps collected data into the given stream
        /// </summary>
        /// <param name="outputStream">the output-stream that will receive the dumped data</param>
        /// <param name="data">the data that must be dumped</param>
        /// <param name="configuration">the dumper-configuiration</param>
        /// <returns>a value indicating whether there was any data available for dumping</returns>
        bool DumpData(Stream outputStream, DynamicResult[] data, DumpConfiguration configuration);
    }
}
